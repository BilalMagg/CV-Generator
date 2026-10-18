import { Component, OnDestroy, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { BulkApplyService } from '@app/services/bulk-apply.service';
import { MailboxService } from '@app/services/mailbox.service';
import { DocumentsService } from '@app/services/documents.service';
import { ContactService } from '@app/services/contact.service';
import { AuthService } from '@app/services/auth.service';
import { ToastService } from '@app/services/toast.service';
import { CvDocumentDto } from '@app/models/document.model';
import { ScheduleTemplateDto } from '@app/models/apply.model';
import { BulkApplyRowDto, BulkApplyRunDto } from '@app/models/bulk-apply.model';
import {
  resolveTemplateVars,
  recipientGreeting,
  TemplateVarValues,
  extractTemplateVars,
  templateVarLabel,
} from '@app/shared/template-vars';

interface CvOption {
  id: string;
  label: string;
}

interface ReviewRow {
  key: string;
  include: boolean;
  companyName: string;
  location: string;
  specialty: string;
  email: string;
  contactName: string;
  subject: string;
  body: string;
  expanded: boolean;
  /** True when this email already exists as a saved contact (pre-unchecked by default). */
  existingContact: boolean;
}

interface SharedVarField {
  token: string;
  label: string;
  hint: string;
}

/** Tokens that are not per-row and therefore need one shared value for the whole batch. */
const SHARED_TOKENS = ['school', 'degree', 'research', 'offer_phrase', 'company_description', 'my_phone'];

const DEFAULT_CAP = 25;
const MAX_ROWS = 200;
const POLL_MS = 1500;

@Component({
  selector: 'app-bulk-apply',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './bulk-apply.component.html',
  styleUrl: './bulk-apply.component.scss',
})
export class BulkApplyComponent implements OnDestroy {
  private bulkSvc = inject(BulkApplyService);
  private mailboxSvc = inject(MailboxService);
  private docsSvc = inject(DocumentsService);
  private contactSvc = inject(ContactService);
  private authSvc = inject(AuthService);
  private toast = inject(ToastService);
  private router = inject(Router);

  step = signal<1 | 2 | 3 | 4>(1);
  readonly defaultCap = DEFAULT_CAP;
  readonly maxRows = MAX_ROWS;
  readonly subjectPlaceholder = 'Candidature — {{company_name}}';
  readonly bodyPlaceholder = 'Bonjour {{recipient_greeting}},\n\nJe me permets de vous contacter…';

  // ── Step 1: paste ──────────────────────────────────────────────────────────────
  pasteText = '';
  extracting = signal(false);
  extractMethod = signal<'' | 'deterministic' | 'llm'>('');
  error = signal('');

  // ── Step 2: review rows ────────────────────────────────────────────────────────
  rows = signal<ReviewRow[]>([]);
  private keySeq = 0;

  // ── Step 3: compose ────────────────────────────────────────────────────────────
  templates = signal<ScheduleTemplateDto[]>([]);
  selectedTemplateId = signal('');
  sharedVars = signal<Record<string, string>>({});
  manualSubject = '';
  manualBody = '';
  cvOptions = signal<CvOption[]>([]);
  selectedCvVersionId = signal('');
  delaySeconds = 5;
  composeMode = signal<'template' | 'manual'>('template');

  // ── Step 4: run / progress ─────────────────────────────────────────────────────
  starting = signal(false);
  run = signal<BulkApplyRunDto | null>(null);
  runId = signal<string | null>(null);
  gmailConnected = signal(false);
  gmailNeedsReauth = signal(false);
  private pollHandle: ReturnType<typeof setTimeout> | null = null;

  selectedTemplate = computed(() =>
    this.templates().find((t) => t.id === this.selectedTemplateId()) ?? null,
  );

  includedRows = computed(() => this.rows().filter((r) => r.include));
  sendableRows = computed(() => this.includedRows().filter((r) => r.email.trim().length > 0));
  overCap = computed(() => this.sendableRows().length > this.defaultCap);

  sharedVarFields = computed<SharedVarField[]>(() => {
    const t = this.selectedTemplate();
    if (!t) return [];
    const tokens = extractTemplateVars(t.subjectTemplate, t.bodyTemplate).filter((tok) =>
      SHARED_TOKENS.includes(tok),
    );
    return tokens.map((token) => ({
      token,
      label: templateVarLabel(token),
      hint: (t.variableDefaults as Record<string, string | undefined> | null)?.[token] ?? '',
    }));
  });

  progressPct = computed(() => {
    const r = this.run();
    if (!r || r.totalRows === 0) return 0;
    return Math.round((r.processedRows / r.totalRows) * 100);
  });

  runFinished = computed(() => {
    const s = this.run()?.status;
    return s === 'completed' || s === 'failed' || s === 'cancelled';
  });

  async ngOnInit() {
    try {
      const res = await this.mailboxSvc.getScheduleTemplates();
      this.templates.set(res.data ?? []);
    } catch {
      /* templates optional */
    }
    try {
      const cvs = (await this.docsSvc.listCvs()).data ?? [];
      const opts: CvOption[] = [];
      for (const cv of cvs as CvDocumentDto[]) {
        for (const v of cv.versions) opts.push({ id: v.id, label: `${cv.title} — v${v.versionNumber}${v.label ? ' · ' + v.label : ''}` });
      }
      this.cvOptions.set(opts);
    } catch {
      /* documents optional */
    }
    try {
      const gmail = await this.mailboxSvc.getGmailStatus();
      this.gmailConnected.set(gmail.connected);
      this.gmailNeedsReauth.set(gmail.needsReauth ?? false);
    } catch {
      /* gmail status optional */
    }
  }

  ngOnDestroy() {
    this.stopPolling();
  }

  // ── Step 1 → 2 ─────────────────────────────────────────────────────────────────

  async extract() {
    if (!this.pasteText.trim()) {
      this.error.set('Paste your list of companies first.');
      return;
    }
    this.error.set('');
    this.extracting.set(true);
    try {
      const res = await this.bulkSvc.extract(this.pasteText);
      const data = res.data;
      if (!res.success || !data) {
        this.error.set(res.message || 'Could not parse the list');
        return;
      }
      if (!data.rows.length) {
        this.error.set('No companies found — check the format or fill rows manually.');
        return;
      }

      const existing = await this.loadExistingEmails();
      this.keySeq = 0;
      this.rows.set(
        data.rows.map((r) => {
          const email = (r.email ?? '').trim();
          const isExisting = !!email && existing.has(email.toLowerCase());
          return {
            key: `r${this.keySeq++}`,
            include: !isExisting,
            companyName: r.companyName ?? '',
            location: r.location ?? '',
            specialty: (r.specialties ?? []).join(', '),
            email,
            contactName: r.contactName ?? '',
            subject: '',
            body: '',
            expanded: false,
            existingContact: isExisting,
          };
        }),
      );
      this.extractMethod.set(data.method);
      this.step.set(2);
      if (existing.size) {
        this.toast.success(`Parsed ${data.rows.length} rows — existing contacts were unchecked`);
      }
    } catch (e: any) {
      this.error.set(e?.message || 'Extraction failed');
    } finally {
      this.extracting.set(false);
    }
  }

  private async loadExistingEmails(): Promise<Set<string>> {
    const set = new Set<string>();
    try {
      const res = await this.contactSvc.getContacts({ page: 1, pageSize: 500 });
      for (const c of res.data?.items ?? []) {
        if (c.email) set.add(c.email.trim().toLowerCase());
      }
    } catch {
      /* dedupe is best-effort */
    }
    return set;
  }

  // ── Step 2: review ─────────────────────────────────────────────────────────────

  toggleAll(value: boolean) {
    this.rows.update((list) => list.map((r) => ({ ...r, include: value })));
  }

  allIncluded(): boolean {
    const list = this.rows();
    return list.length > 0 && list.every((r) => r.include);
  }

  toggleRow(key: string) {
    this.rows.update((list) => list.map((r) => (r.key === key ? { ...r, include: !r.include } : r)));
  }

  toggleExpand(key: string) {
    this.rows.update((list) => list.map((r) => (r.key === key ? { ...r, expanded: !r.expanded } : r)));
  }

  removeRow(key: string) {
    this.rows.update((list) => list.filter((r) => r.key !== key));
  }

  addRow() {
    this.rows.update((list) => [
      ...list,
      {
        key: `r${this.keySeq++}`,
        include: true,
        companyName: '',
        location: '',
        specialty: '',
        email: '',
        contactName: '',
        subject: '',
        body: '',
        expanded: true,
        existingContact: false,
      },
    ]);
  }

  goCompose() {
    if (!this.sendableRows().length) {
      this.error.set('Select at least one row that has an email address.');
      return;
    }
    this.error.set('');
    this.prefillSharedVars();
    this.step.set(3);
  }

  backToPaste() {
    this.step.set(1);
  }

  backToReview() {
    this.step.set(2);
  }

  // ── Step 3: compose ────────────────────────────────────────────────────────────

  private prefillSharedVars() {
    const t = selected(this.templates(), this.selectedTemplateId());
    const d = (t?.variableDefaults ?? {}) as Record<string, string | undefined>;
    const next: Record<string, string> = {};
    for (const f of this.sharedVarFields()) next[f.token] = this.sharedVars()[f.token] ?? d[f.token] ?? '';
    this.sharedVars.set(next);
  }

  onTemplateChange(id: string) {
    this.selectedTemplateId.set(id);
    this.prefillSharedVars();
    this.applyTemplateToAll(true);
  }

  setSharedVar(token: string, value: string) {
    this.sharedVars.update((v) => ({ ...v, [token]: value }));
  }

  private rowValues(row: ReviewRow): TemplateVarValues {
    const t = selected(this.templates(), this.selectedTemplateId());
    const d = (t?.variableDefaults ?? {}) as Record<string, string | undefined>;
    const user = this.authSvc.currentUser();
    const name = `${user?.firstName ?? ''} ${user?.lastName ?? ''}`.trim();
    const sv = this.sharedVars();
    return {
      company_name: row.companyName,
      company_description: sv['company_description'] ?? '',
      recipient_name: row.contactName,
      recipient_greeting: recipientGreeting(row.contactName),
      school: sv['school'] ?? d['school'] ?? '',
      degree: sv['degree'] ?? d['degree'] ?? '',
      research: sv['research'] ?? d['research'] ?? '',
      offer_phrase: sv['offer_phrase'] ?? d['offer_phrase'] ?? '',
      my_name: name,
      my_email: user?.email ?? '',
      my_phone: sv['my_phone'] ?? '',
      domaine: row.specialty,
      web_company: '',
    };
  }

  applyTemplateToAll(silent = false) {
    const t = selected(this.templates(), this.selectedTemplateId());
    if (!t) {
      if (!silent) this.error.set('Pick a template first.');
      return;
    }
    this.rows.update((list) =>
      list.map((r) =>
        r.include
          ? {
              ...r,
              subject: resolveTemplateVars(t.subjectTemplate, this.rowValues(r)),
              body: resolveTemplateVars(t.bodyTemplate, this.rowValues(r)),
            }
          : r,
      ),
    );
    this.error.set('');
    if (!silent) this.toast.success(`Template applied to ${this.includedRows().length} rows`);
  }

  applyManualToAll() {
    const subject = this.manualSubject.trim();
    const body = this.manualBody;
    if (!subject || !body.trim()) {
      this.error.set('Enter a subject and body to apply.');
      return;
    }
    this.rows.update((list) =>
      list.map((r) =>
        r.include
          ? {
              ...r,
              subject: resolveTemplateVars(subject, this.rowValues(r)),
              body: resolveTemplateVars(body, this.rowValues(r)),
            }
          : r,
      ),
    );
    this.error.set('');
    this.toast.success(`Message applied to ${this.includedRows().length} rows`);
  }

  setComposeMode(mode: 'template' | 'manual') {
    this.composeMode.set(mode);
    this.error.set('');
  }

  allRowsHaveMessage(): boolean {
    return this.includedRows().every((r) => r.subject.trim() && r.body.trim());
  }

  startRun() {
    if (this.gmailNeedsReauth()) {
      this.error.set('Gmail session expired — reconnect Gmail in Mailbox → Settings before sending.');
      return;
    }
    if (!this.allRowsHaveMessage()) {
      this.error.set('Every selected row needs a subject and body. Apply a template or write a message first.');
      return;
    }
    const rows = this.sendableRows().map((r) => ({
      companyName: r.companyName.trim(),
      location: r.location.trim() || undefined,
      specialty: r.specialty.trim() || undefined,
      recipientEmail: r.email.trim(),
      recipientName: r.contactName.trim() || undefined,
      subject: r.subject,
      body: r.body,
    }));
    if (!rows.length) {
      this.error.set('No sendable rows.');
      return;
    }

    void this.createRun(rows);
  }

  private async createRun(rows: {
    companyName: string;
    location?: string;
    specialty?: string;
    recipientEmail: string;
    recipientName?: string;
    subject: string;
    body: string;
  }[]) {
    this.error.set('');
    this.starting.set(true);
    try {
      const res = await this.bulkSvc.createRun({
        cvVersionId: this.selectedCvVersionId() || undefined,
        delaySeconds: Math.max(0, Math.min(120, Number(this.delaySeconds) || 0)),
        rows,
      });
      if (!res.success || !res.data) {
        this.error.set(res.message || 'Could not start the run');
        return;
      }
      this.runId.set(res.data.runId);
      this.step.set(4);
      await this.refreshRun();
      this.schedulePoll();
    } catch (e: any) {
      this.error.set(e?.message || 'Could not start the run');
    } finally {
      this.starting.set(false);
    }
  }

  // ── Step 4: progress ───────────────────────────────────────────────────────────

  private schedulePoll() {
    this.stopPolling();
    if (this.runFinished()) return;
    this.pollHandle = setTimeout(async () => {
      await this.refreshRun();
      this.schedulePoll();
    }, POLL_MS);
  }

  private stopPolling() {
    if (this.pollHandle) {
      clearTimeout(this.pollHandle);
      this.pollHandle = null;
    }
  }

  async refreshRun() {
    const id = this.runId();
    if (!id) return;
    try {
      const res = await this.bulkSvc.getRun(id);
      if (res.success && res.data) this.run.set(res.data);
    } catch {
      /* transient poll failure — try again next tick */
    }
  }

  async cancelRun() {
    const id = this.runId();
    if (!id) return;
    try {
      await this.bulkSvc.cancel(id);
      this.toast.success('Cancellation requested');
      await this.refreshRun();
    } catch (e: any) {
      this.toast.error(e?.message || 'Could not cancel');
    }
  }

  trackByKey(_i: number, row: ReviewRow): string {
    return row.key;
  }

  trackRowId(_i: number, row: BulkApplyRowDto): string {
    return row.id;
  }

  statusLabel(s: string): string {
    switch (s) {
      case 'pending': return 'Queued';
      case 'running': return 'Sending';
      case 'sending': return 'Sending';
      case 'sent': return 'Sent';
      case 'failed': return 'Failed';
      case 'skipped': return 'Skipped';
      case 'completed': return 'Completed';
      case 'cancelled': return 'Cancelled';
      default: return s;
    }
  }

  runStatusClass(s: string | undefined): string {
    return `status-${s ?? 'pending'}`;
  }

  openApplication(row: BulkApplyRowDto) {
    if (row.applicationId) this.router.navigate(['/applications', row.applicationId]);
  }

  newRun() {
    this.stopPolling();
    this.run.set(null);
    this.runId.set(null);
    this.rows.set([]);
    this.pasteText = '';
    this.extractMethod.set('');
    this.selectedTemplateId.set('');
    this.sharedVars.set({});
    this.manualSubject = '';
    this.manualBody = '';
    this.selectedCvVersionId.set('');
    this.error.set('');
    this.step.set(1);
  }

  goApplications() {
    this.router.navigate(['/applications/kanban']);
  }
}

/** Tiny local lookup helper (keeps the template call sites terse). */
function selected(list: ScheduleTemplateDto[], id: string): ScheduleTemplateDto | undefined {
  return list.find((t) => t.id === id);
}
