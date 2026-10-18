import { Component, effect, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CompanyService,
  CompanyCredentialDto,
  UpsertCompanyCredentialDto,
} from '@app/services/company.service';
import { ConfirmService } from '@app/services/confirm.service';
import { ToastService } from '@app/services/toast.service';

/**
 * Portal/account logins kept per company. Renders the list plus an inline
 * add/edit form; passwords are stored plaintext but masked in the UI with a
 * reveal + copy toggle. Reusable on the company detail page and the create
 * application sidebar.
 */
@Component({
  selector: 'app-company-credentials',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './company-credentials.component.html',
  styleUrl: './company-credentials.component.scss',
})
export class CompanyCredentialsComponent {
  private readonly companies = inject(CompanyService);
  private readonly confirm = inject(ConfirmService);
  private readonly toast = inject(ToastService);

  companyId = input.required<string>();
  /** Tighter layout for narrow sidebars. */
  compact = input(false);

  credentials = signal<CompanyCredentialDto[]>([]);
  loading = signal(false);
  error = signal<string | null>(null);

  // ── Inline form ─────────────────────────────────────────────────────────────
  formOpen = signal(false);
  editingId = signal<string | null>(null);
  fLabel = signal('');
  fUrl = signal('');
  fUsername = signal('');
  fPassword = signal('');
  fNotes = signal('');
  saving = signal(false);

  // ── Reveal / copy feedback ──────────────────────────────────────────────────
  revealed = signal<Set<string>>(new Set());
  copiedKey = signal<string | null>(null);

  constructor() {
    effect(() => {
      const id = this.companyId();
      this.formOpen.set(false);
      this.editingId.set(null);
      if (id) void this.load(id);
    });
  }

  async load(id = this.companyId()): Promise<void> {
    if (!id) return;
    this.loading.set(true);
    this.error.set(null);
    try {
      const res = await this.companies.getCompanyCredentials(id);
      if (res.success && res.data) this.credentials.set(res.data);
      else this.error.set(res.message || 'Failed to load credentials');
    } catch {
      this.error.set('Failed to load credentials');
    } finally {
      this.loading.set(false);
    }
  }

  isRevealed(id: string): boolean {
    return this.revealed().has(id);
  }

  toggleReveal(id: string): void {
    this.revealed.update(set => {
      const next = new Set(set);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  async copy(text: string | null | undefined, key: string): Promise<void> {
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      this.copiedKey.set(key);
      setTimeout(() => { if (this.copiedKey() === key) this.copiedKey.set(null); }, 1500);
    } catch {
      this.toast.error('Could not copy to clipboard');
    }
  }

  // ── Form actions ────────────────────────────────────────────────────────────

  startAdd(): void {
    this.editingId.set(null);
    this.fLabel.set('');
    this.fUrl.set('');
    this.fUsername.set('');
    this.fPassword.set('');
    this.fNotes.set('');
    this.formOpen.set(true);
  }

  startEdit(c: CompanyCredentialDto): void {
    this.editingId.set(c.id);
    this.fLabel.set(c.label ?? '');
    this.fUrl.set(c.url ?? '');
    this.fUsername.set(c.username ?? '');
    this.fPassword.set(c.password ?? '');
    this.fNotes.set(c.notes ?? '');
    this.formOpen.set(true);
  }

  cancelForm(): void {
    this.formOpen.set(false);
    this.editingId.set(null);
  }

  private toDto(): UpsertCompanyCredentialDto {
    return {
      label: this.fLabel().trim() || undefined,
      url: this.fUrl().trim() || undefined,
      username: this.fUsername().trim() || undefined,
      password: this.fPassword() || undefined,
      notes: this.fNotes().trim() || undefined,
    };
  }

  async save(): Promise<void> {
    const dto = this.toDto();
    if (!dto.label && !dto.url && !dto.username && !dto.password && !dto.notes) {
      this.toast.error('Add at least a username, password or portal URL');
      return;
    }
    this.saving.set(true);
    try {
      const id = this.companyId();
      const editing = this.editingId();
      const res = editing
        ? await this.companies.updateCompanyCredential(id, editing, dto)
        : await this.companies.createCompanyCredential(id, dto);
      if (!res.success) {
        this.toast.error(res.message || 'Failed to save credential');
        return;
      }
      this.toast.success(editing ? 'Credential updated' : 'Credential added');
      this.cancelForm();
      await this.load(id);
    } catch (err) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.toast.error(msg || 'Failed to save credential');
    } finally {
      this.saving.set(false);
    }
  }

  async remove(c: CompanyCredentialDto): Promise<void> {
    const ok = await this.confirm.confirm({
      title: 'Delete credential',
      message: `Delete the "${c.label || c.username || 'account'}" credential?`,
      confirmText: 'Delete',
      variant: 'danger',
    });
    if (!ok) return;
    try {
      await this.companies.deleteCompanyCredential(this.companyId(), c.id);
      this.toast.success('Credential deleted');
      if (this.editingId() === c.id) this.cancelForm();
      await this.load();
    } catch {
      this.toast.error('Failed to delete credential');
    }
  }
}
