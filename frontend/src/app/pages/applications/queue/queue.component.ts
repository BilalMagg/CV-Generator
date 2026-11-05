import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApplyQueueService } from '@app/services/apply-queue.service';
import { DocumentsService } from '@app/services/documents.service';
import { MailboxService } from '@app/services/mailbox.service';
import { AuthService } from '@app/services/auth.service';
import { ToastService } from '@app/services/toast.service';
import { ApplyQueueItemDto, SaveApplyQueueItemDto } from '@app/models/apply-queue.model';
import { ScheduleTemplateDto } from '@app/models/apply.model';
import { CV_PICKER_FALLBACK_HINT, buildCvOptions } from '@app/shared/cv-options';
import {
  extractTemplateVars,
  recipientGreeting,
  resolveTemplateVars,
  templateVarLabel,
  TemplateVarValues,
} from '@app/shared/template-vars';

/** Tokens resolved automatically from the form fields / user profile — no input shown for them. */
const AUTO_TOKENS = new Set(['recipient_name', 'recipient_greeting', 'company_name', 'my_name', 'my_email']);

@Component({
  selector: 'app-apply-queue',
  imports: [FormsModule, RouterLink],
  templateUrl: './queue.component.html',
  styleUrl: './queue.component.scss',
})
export class ApplyQueueComponent {
  private readonly queueSvc = inject(ApplyQueueService);
  private readonly docsSvc = inject(DocumentsService);
  private readonly mailboxSvc = inject(MailboxService);
  private readonly authSvc = inject(AuthService);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly items = signal<ApplyQueueItemDto[]>([]);
  readonly filter = signal<'pending' | 'sent' | 'failed' | 'all'>('pending');
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly selected = signal<Set<string>>(new Set());
  readonly executing = signal(false);
  /** True when the last execute failed because of Gmail auth — the error banner then shows a Connect button. */
  readonly reauth = signal(false);
  /** Back on this page after the Google OAuth round-trip. */
  readonly gmailConnectUrl = '/api/gmail/connect?returnUrl=%2Fapplications%2Fqueue';

  readonly cvOptions = signal<{ id: string; label: string }[]>([]);
  /** True while listCvs returned active versions only; false = fallback to all + hint. */
  readonly cvOptionsOnlyActive = signal(true);
  readonly cvPickerHint = CV_PICKER_FALLBACK_HINT;
  readonly selectedCvVersionId = signal('');
  readonly delaySeconds = signal(5);

  // ── "Add to queue" form ────────────────────────────────────────────────────────
  readonly addOpen = signal(false);
  readonly addSaving = signal(false);
  readonly addError = signal<string | null>(null);
  readonly templates = signal<ScheduleTemplateDto[]>([]);
  readonly formEmail = signal('');
  readonly formRecipientName = signal('');
  readonly formCompany = signal('');
  readonly formCompanyTouched = signal(false);
  readonly formPosition = signal('');
  readonly formTemplateId = signal('');
  readonly formCvVersionId = signal('');
  readonly formSubject = signal('');
  readonly formBody = signal('');
  readonly formVars = signal<Record<string, string>>({});

  readonly formTemplate = computed(
    () => this.templates().find((t) => t.id === this.formTemplateId()) ?? null,
  );

  /** Tokens the user fills manually (auto-resolved tokens like recipient/company/my_* are excluded). */
  readonly formVarFields = computed(() => {
    const t = this.formTemplate();
    if (!t) return [];
    const tokens = extractTemplateVars(t.subjectTemplate, t.bodyTemplate).filter(
      (tok) => !AUTO_TOKENS.has(tok),
    );
    return tokens.map((token) => ({
      token,
      label: templateVarLabel(token),
    }));
  });

  /** Company that will actually be used (auto-derived from the email domain when left empty). */
  readonly formEffectiveCompany = computed(() => {
    const manual = this.formCompany().trim();
    if (manual) return manual;
    return this.deriveCompanyFromEmail(this.formEmail());
  });

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
    await Promise.all([this.load(), this.ensureCvsLoaded(), this.ensureTemplatesLoaded()]);
  }

  private templatesLoaded = false;
  private async ensureTemplatesLoaded(): Promise<void> {
    if (this.templatesLoaded) return;
    try {
      this.templates.set((await this.mailboxSvc.getScheduleTemplates()).data ?? []);
      this.templatesLoaded = true;
    } catch {
      /* templates optional — the form still works manually */
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.reauth.set(false);
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
      const built = buildCvOptions(cvs);
      const opts = built.options;
      this.cvOptions.set(opts);
      this.cvOptionsOnlyActive.set(built.onlyActive);
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
      const msg = this.errMsg(e) || 'Could not start the run';
      // Gmail auth failures get an inline Connect button on this page instead of a
      // dead-end to the Mailbox settings.
      this.reauth.set(/gmail/i.test(msg));
      this.error.set(msg);
      this.toast.error(msg);
      this.executing.set(false);
    }
  }

  cvLabel(item: ApplyQueueItemDto): string {
    if (!item.cvVersionId) return 'Default (run)';
    return this.cvOptions().find((o) => o.id === item.cvVersionId)?.label ?? 'CV';
  }

  // ── "Add to queue" form ────────────────────────────────────────────────────────

  openAdd(): void {
    this.resetAddForm();
    this.addError.set(null);
    this.addSaving.set(false);
    this.addOpen.set(true);
  }

  closeAdd(): void {
    this.addOpen.set(false);
  }

  private resetAddForm(): void {
    this.formEmail.set('');
    this.formRecipientName.set('');
    this.formCompany.set('');
    this.formCompanyTouched.set(false);
    this.formPosition.set('');
    this.formTemplateId.set('');
    this.formCvVersionId.set('');
    this.formSubject.set('');
    this.formBody.set('');
    this.formVars.set({});
  }

  /** Typing an email auto-fills the (still-editable) company field when the user left it untouched. */
  onAddEmailChange(value: string): void {
    this.formEmail.set(value ?? '');
    if (!this.formCompanyTouched()) {
      this.formCompany.set(this.deriveCompanyFromEmail(this.formEmail()));
    }
  }

  onAddCompanyInput(value: string): void {
    this.formCompany.set(value ?? '');
    this.formCompanyTouched.set(true);
  }

  /** Picking a template prefills subject/body (resolved) and adopts its CV version. */
  onAddTemplateChange(id: string): void {
    this.formTemplateId.set(id || '');
    this.formVars.set({}); // fresh template = fresh defaults
    this.reResolveFromTemplate(true);
  }

  onAddTokenChange(token: string, value: string): void {
    this.formVars.update((v) => ({ ...v, [token]: value }));
    this.reResolveFromTemplate(false);
  }

  /** Recipient name feeds the {{recipient_name}} / {{recipient_greeting}} tokens. */
  onAddNameChange(value: string): void {
    this.formRecipientName.set(value ?? '');
    this.reResolveFromTemplate(false);
  }

  /** Values fed to resolveTemplateVars — same resolution rules as the bulk-apply wizard. */
  private resolveValues(): TemplateVarValues {
    const t = this.formTemplate();
    const d = (t?.variableDefaults ?? {}) as Record<string, string | undefined>;
    const vars = this.formVars();
    const user = this.authSvc.currentUser();
    const name = `${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim();
    const fields = this.formVarFields().map((f) => f.token);
    const values: TemplateVarValues = {
      company_name: this.formEffectiveCompany(),
      company_description: '',
      recipient_name: this.formRecipientName().trim(),
      recipient_greeting: recipientGreeting(this.formRecipientName()),
      school: '',
      degree: '',
      research: '',
      offer_phrase: '',
      my_name: name,
      my_email: user?.email ?? '',
      my_phone: '',
    };
    for (const token of fields) {
      values[token] = vars[token] ?? d[token] ?? '';
    }
    return values;
  }

  /** Re-fills subject/body from the selected template with the current values (kept editable afterwards). */
  private reResolveFromTemplate(adoptCv: boolean): void {
    const t = this.formTemplate();
    if (!t) return;
    const d = (t.variableDefaults ?? {}) as Record<string, string | undefined>;
    // Prefill any token input that has no value yet (first open), otherwise keep user edits.
    const next: Record<string, string> = { ...this.formVars() };
    for (const f of this.formVarFields()) {
      if (!(f.token in next)) next[f.token] = d[f.token] ?? '';
    }
    this.formVars.set(next);
    this.formSubject.set(resolveTemplateVars(t.subjectTemplate, this.resolveValues()));
    this.formBody.set(resolveTemplateVars(t.bodyTemplate, this.resolveValues()));
    if (adoptCv) {
      this.formCvVersionId.set(t.cvVersionId ?? '');
    }
  }

  async submitAdd(): Promise<void> {
    const email = this.formEmail().trim();
    const subject = this.formSubject().trim();
    if (!/^([^\s@]+)@([^\s@]+)\.[^\s@]+$/.test(email)) {
      this.addError.set('Enter a valid recipient email');
      return;
    }
    if (!subject) {
      this.addError.set('Subject is required — pick a template or type one');
      return;
    }
    const dto: SaveApplyQueueItemDto = {
      companyName: this.formCompany().trim() || this.formEffectiveCompany(),
      positionTitle: this.formPosition().trim() || undefined,
      recipientEmail: email,
      recipientName: this.formRecipientName().trim() || undefined,
      subject,
      body: this.formBody() || undefined,
      cvVersionId: this.formCvVersionId() || undefined,
      source: 'manual',
    };
    this.addSaving.set(true);
    this.addError.set(null);
    try {
      await this.queueSvc.save(dto);
      this.toast.success(`Queued — ${dto.companyName || email}`);
      this.addOpen.set(false);
      this.addSaving.set(false);
      await this.load();
    } catch (e: unknown) {
      this.addError.set(this.errMsg(e) || 'Could not add to the queue');
      this.addSaving.set(false);
    }
  }

  /**
   * Derives a company display name from an email domain when the name is missing:
   * test@companyname.com → "Companyname", hr@recruitment.capgemini.com → "Capgemini".
   * Mirrors BulkApplyService.DeriveCompanyName (backend authoritative at queue creation).
   */
  private deriveCompanyFromEmail(email: string): string {
    const host = (email ?? '').trim().toLowerCase().split('@')[1] ?? '';
    if (!host || !host.includes('.')) return '';
    const generic = new Set(['mail', 'smtp', 'hr', 'recruitment', 'recrutement', 'recrut', 'careers', 'jobs', 'contact', 'info']);
    const parts = host.split('.').filter(Boolean);
    if (parts.length < 2) return '';
    // Candidates = everything up to the last TLD (2-level TLDs like co.uk: drop the extras)
    const candidates = parts.slice(0, -1);
    if (candidates.length > 1 && ['co', 'com', 'org', 'net', 'gov', 'edu'].includes(candidates[candidates.length - 1])) {
      candidates.pop();
    }
    const filterGeneric = (tokens: string[]) => tokens.find((s) => s && !generic.has(s));
    const seg = filterGeneric(candidates) ?? candidates[0] ?? '';
    const words = seg.replace(/_/g, '-').split('-').filter(Boolean);
    if (!words.length) return '';
    return words.map((w) => w[0].toUpperCase() + w.slice(1)).join(' ');
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
