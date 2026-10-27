import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApplyQueueService } from '@app/services/apply-queue.service';
import { DocumentsService } from '@app/services/documents.service';
import { ToastService } from '@app/services/toast.service';
import { ApplyQueueItemDto } from '@app/models/apply-queue.model';

@Component({
  selector: 'app-apply-queue',
  imports: [FormsModule, RouterLink],
  templateUrl: './queue.component.html',
  styleUrl: './queue.component.scss',
})
export class ApplyQueueComponent {
  private readonly queueSvc = inject(ApplyQueueService);
  private readonly docsSvc = inject(DocumentsService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly items = signal<ApplyQueueItemDto[]>([]);
  readonly filter = signal<'pending' | 'sent' | 'failed' | 'all'>('pending');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selected = signal<Set<string>>(new Set());
  readonly executing = signal(false);

  readonly cvOptions = signal<{ id: string; label: string }[]>([]);
  readonly selectedCvVersionId = signal('');
  readonly delaySeconds = signal(5);

  readonly shown = computed(() => {
    const f = this.filter();
    if (f === 'all') return this.items();
    return this.items().filter((i) => i.status === f);
  });

  readonly counts = computed(() => {
    const items = this.items();
    return {
      pending: items.filter((i) => i.status === 'pending').length,
      sent: items.filter((i) => i.status === 'sent').length,
      failed: items.filter((i) => i.status === 'failed').length,
      all: items.length,
    };
  });

  readonly pendingCount = computed(() => this.counts().pending);
  readonly allSelected = computed(() => {
    const list = this.shown().filter((i) => i.status === 'pending');
    if (list.length === 0) return false;
    const sel = this.selected();
    return list.every((i) => sel.has(i.id));
  });
  readonly selectedCount = computed(
    () => this.shown().filter((i) => this.selected().has(i.id) && i.status === 'pending').length,
  );

  readonly expanded = signal<Set<string>>(new Set());
  readonly hasRequestable = computed(() => this.pendingCount() + this.counts().failed > 0);

  /** Selected ids that can actually execute (pending or retryable-failed with... backend decides). */
  readonly executableIds = computed(() =>
    this.shown().filter((i) => this.selected().has(i.id) && (i.status === 'pending' || i.status === 'failed')).map((i) => i.id),
  );

  async ngOnInit(): Promise<void> {
    await Promise.all([this.load(), this.ensureCvsLoaded()]);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const res = await this.queueSvc.list('all');
      this.items.set(res.data ?? []);
      // Default selection = ALL pending items (kept when a simple reload happens).
      const pending = (res.data ?? []).filter((i) => i.status === 'pending').map((i) => i.id);
      this.selected.set(new Set(pending));
    } catch (e: unknown) {
      this.error.set(this.errMsg(e) || 'Could not load the queue');
    } finally {
      this.loading.set(false);
    }
  }

  private cvLoaded = false;
  private async ensureCvsLoaded(): Promise<void> {
    if (this.cvLoaded) return;
    try {
      const cvs = (await this.docsSvc.listCvs()).data ?? [];
      const opts: { id: string; label: string }[] = [];
      for (const cv of cvs) {
        for (const v of cv.versions) {
          opts.push({
            id: v.id,
            label: `${cv.title} — v${v.versionNumber}${v.label ? ' · ' + v.label : ''}`,
          });
        }
      }
      this.cvOptions.set(opts);
      this.cvLoaded = true;
    } catch {
      /* documents optional */
    }
  }

  setFilter(f: 'pending' | 'sent' | 'failed' | 'all'): void {
    this.filter.set(f);
    // Keep pending items selected on filter switch so Execute stays one click away.
    if (f === 'pending') {
      const pending = this.items().filter((i) => i.status === 'pending').map((i) => i.id);
      this.selected.set(new Set(pending));
    }
  }

  toggle(id: string): void {
    const set = new Set(this.selected());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.selected.set(set);
  }

  toggleAll(): void {
    if (this.allSelected()) {
      const set = new Set(this.selected());
      for (const i of this.shown()) if (i.status === 'pending') set.delete(i.id);
      this.selected.set(set);
    } else {
      const sel = new Set(this.selected());
      for (const i of this.shown()) if (i.status === 'pending') sel.add(i.id);
      this.selected.set(sel);
    }
  }

  isSelected(id: string): boolean {
    return this.selected().has(id);
  }

  toggleExpand(id: string): void {
    const set = new Set(this.expanded());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.expanded.set(set);
  }

  isExpanded(id: string): boolean {
    return this.expanded().has(id);
  }

  async removeItem(item: ApplyQueueItemDto): Promise<void> {
    try {
      await this.queueSvc.remove(item.id);
      this.toast.success('Removed from the queue');
      await this.load();
    } catch (e: unknown) {
      this.toast.error(this.errMsg(e) || 'Could not remove the item');
    }
  }

  async execute(): Promise<void> {
    const ids = this.executableIds();
    if (ids.length === 0) {
      this.toast.error('Select at least one queued apply');
      return;
    }
    this.executing.set(true);
    try {
      const res = await this.queueSvc.execute(ids, this.selectedCvVersionId() || undefined, this.delaySeconds());
      const runId = res.data?.runId;
      if (runId) {
        this.toast.success(
          `Run started — ${res.data?.totalRows ?? ids.length} appl${(res.data?.totalRows ?? ids.length) === 1 ? 'y' : 'ies'} being sent`,
        );
        this.router.navigate(['/applications/bulk-apply'], { queryParams: { run: runId } });
      } else {
        this.toast.error(res.message || 'Could not start the run');
        this.executing.set(false);
      }
    } catch (e: unknown) {
      this.toast.error(this.errMsg(e) || 'Could not start the run');
      this.executing.set(false);
    }
  }

  cvLabel(item: ApplyQueueItemDto): string {
    if (!item.cvVersionId) return 'Default (run)';
    return this.cvOptions().find((o) => o.id === item.cvVersionId)?.label ?? 'CV';
  }

  private errMsg(e: unknown): string {
    const err = e as { message?: string; error?: { message?: string } } | null;
    return err?.error?.message ?? err?.message ?? '';
  }

  fmtRelative(iso?: string | null): string {
    if (!iso) return '—';
    const diffMs = Date.now() - new Date(iso).getTime();
    const mins = Math.round(Math.abs(diffMs) / 60000);
    let label: string;
    if (mins < 1) label = 'less than a minute';
    else if (mins < 60) label = `${mins} min`;
    else if (mins < 60 * 24) label = `${Math.round(mins / 60)} h`;
    else label = `${Math.round(mins / (60 * 24))} d`;
    return `${label} ago`;
  }

  fmtDate(iso?: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }
}
