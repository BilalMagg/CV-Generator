import { Component, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { BulkApplyService } from '@app/services/bulk-apply.service';
import { ApplyQueueService } from '@app/services/apply-queue.service';
import { MailboxService } from '@app/services/mailbox.service';
import { DocumentsService } from '@app/services/documents.service';
import { ContactService } from '@app/services/contact.service';
import { CompanyService } from '@app/services/company.service';
import { AuthService } from '@app/services/auth.service';
import { ToastService } from '@app/services/toast.service';
import { HttpErrorResponse } from '@angular/common/http';
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
  /** Template that produced subject/body (empty for manual edits) — lets a row be switched independently. */
  templateId: string;
  /** Per-row CV version; falls back to the batch CV when empty. */
  cvVersionId: string;
  expanded: boolean;
  /** True when this email already exists as a saved contact (pre-unchecked by default). */
  existingContact: boolean;
  /** True when a company with this (normalized) name is already saved. */
  existingCompany: boolean;
  /** True when this row's company was saved from the review screen. */
  companySaved: boolean;
  /** True when this row's contact was saved from the review screen. */
  contactSaved: boolean;
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

/** localStorage key for the in-progress batch draft (survives reloads/crashes). */
const DRAFT_KEY = 'propel.bulk-apply.draft.v1';
const DRAFT_DEBOUNCE_MS = 400;

/** Sample list for dry-runs — plus-addressed to the signed-in user's own inbox. */
function buildSampleList(userEmail: string): string {
  const base = userEmail.includes('@') ? userEmail.replace(/\+.*@/, '@') : 'YOURMAIL@gmail.com';
  const at = base.indexOf('@');
  const alias = (n: number) => (at > 0 ? `${base.slice(0, at)}+bulk${n}${base.slice(at)}` : `test${n}@example.com`);
  const rows = [
    ['Acme Testing', 'Casablanca', 'Test HR 1', 'Cloud, DevOps'],
    ['Beta QA Corp', 'Rabat', 'Test HR 2', 'Data Science, AI'],
    ['Gamma Sandbox', 'Tangier', 'Test HR 3', 'Web Development, Angular'],
    ['Delta Dryrun', 'Marrakech', 'Test HR 4', 'Cybersecurity'],
    ['Epsilon Trial', 'Agadir', '', 'Mobile, Flutter'],
    ['Zeta Mock Ltd', 'Fès', 'Test HR 6', 'Backend, Java, Spring'],
    ['Eta Dummy SA', 'Oujda', '', 'Data Engineering'],
    ['Theta Simul', 'Kenitra', 'Test HR 8', 'QA, Testing'],
    ['Iota Fake Co', 'Salé', '', 'ML, Python'],
    ['Kappa Example', 'El Jadida', 'Test HR 10', 'DevOps, Kubernetes'],
  ];
  return rows
    .map(([co, loc, contact, spec], i) => {
      const lines = [`${i + 1}. ${co} — ${loc}`];
      if (contact) lines.push(`   Contact: ${contact}`);
      lines.push(`   Email: ${alias(i + 1)}`);
      lines.push(`   Spécialités: ${spec}`);
      return lines.join('\n');
    })
    .join('\n\n');
}

interface BulkDraft {
  v: number;
  savedAt: number;
  step: number;
  pasteText: string;
  rows: ReviewRow[];
  keySeq: number;
  selectedTemplateId: string;
  defaultTemplateId?: string;
  sharedVars: Record<string, string>;
  manualSubject: string;
  manualBody: string;
  selectedCvVersionId: string;
  delaySeconds: number;
  composeMode: 'template' | 'manual';
  extractMethod: '' | 'deterministic' | 'llm';
  aiMapTemplates?: boolean;
  mapTemplateIds?: string[];
}

@Component({
  selector: 'app-bulk-apply',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './bulk-apply.component.html',
  styleUrl: './bulk-apply.component.scss',
})
export class BulkApplyComponent implements OnDestroy {
  private bulkSvc = inject(BulkApplyService);
  private queueSvc = inject(ApplyQueueService);
  private route = inject(ActivatedRoute);
  private mailboxSvc = inject(MailboxService);
  private docsSvc = inject(DocumentsService);
  private contactSvc = inject(ContactService);
  private companySvc = inject(CompanyService);
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

  // ── Step 1: AI template mapping (optional) ─────────────────────────────────────
  /** When true, after extraction the AI picks a template per row from the chosen subset. */
  aiMapTemplates = signal(false);
  /** Template ids the AI is allowed to choose from (empty = all templates). */
  mapTemplateIds = signal<string[]>([]);
  mappingMethod = signal<'' | 'ai'>('');

  // ── Step 2: review rows ────────────────────────────────────────────────────────
  rows = signal<ReviewRow[]>([]);
  private keySeq = 0;

  // ── Step 2: save companies/contacts from review ────────────────────────────────
  savingCompanyKey = signal<string | null>(null);
  savingContactKey = signal<string | null>(null);
  savingAllCompanies = signal(false);
  savingAllContacts = signal(false);
  /** Normalized names/emails already known to exist (seeded at extract, grown on save). */
  private knownCompanyNames = new Set<string>();
  private knownContactEmails = new Set<string>();

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
  /** Fallback template applied only to rows that have no template/message yet. */
  defaultTemplateId = signal('');

  // ── Step 4: run / progress ─────────────────────────────────────────────────────
  starting = signal(false);
  run = signal<BulkApplyRunDto | null>(null);
  runId = signal<string | null>(null);
  gmailConnected = signal(false);
  gmailNeedsReauth = signal(false);
  cancelling = signal(false);
  retrying = signal(false);
  /** True when the run has not advanced for a while — the UI offers a cancel/retry escape. */
  stalled = signal(false);
  private pollHandle: ReturnType<typeof setTimeout> | null = null;
  private lastProgressAt = 0;
  private lastProcessed = -1;

  // ── Draft (localStorage auto-save) ─────────────────────────────────────────────
  draftRestored = signal(false);
  draftSavedAt = signal<number | null>(null);
  private persistHandle: ReturnType<typeof setTimeout> | null = null;
  private persistEnabled = false;
  private draftSuppressed = false;

  constructor() {
    // Persist whenever signal-backed state changes (rows, template, mode, step…).
    // Plain fields (paste text, manual subject/body, delay) are covered by the
    // (input) hook on the page container, since ngModel writes them in place.
    effect(() => {
      this.step();
      this.rows();
      this.selectedTemplateId();
      this.sharedVars();
      this.selectedCvVersionId();
      this.composeMode();
      this.extractMethod();
      this.schedulePersist();
    });
  }

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

  /** Number of rows that can be retried (only meaningful once the run is terminal). */
  failedRows = computed(() => (this.run()?.rows ?? []).filter((row) => row.status === 'failed').length);

  async ngOnInit() {
    this.restoreDraft();
    this.persistEnabled = true;
    // Queue hand-off: /applications/bulk-apply?run=<id> shows an existing run's progress
    // (redirect target of the Apply Queue execute action).
    const runParam = this.route.snapshot.queryParamMap.get('run');
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
    if (runParam) {
      const res = await this.bulkSvc.getRun(runParam);
      if (res.success && res.data) {
        this.runId.set(runParam);
        this.lastProcessed = -1;
        this.lastProgressAt = Date.now();
        this.stalled.set(false);
        this.draftSuppressed = true;
        this.clearDraft();
        this.step.set(4);
        this.run.set(res.data);
        this.schedulePoll();
      }
    }
  }

  ngOnDestroy() {
    this.stopPolling();
    if (this.persistHandle) {
      clearTimeout(this.persistHandle);
      this.persistHandle = null;
      if (this.persistEnabled) this.persistDraft();
    }
  }

  // ── Draft persistence ──────────────────────────────────────────────────────────

  private restoreDraft() {
    try {
      const raw = localStorage.getItem(DRAFT_KEY);
      if (!raw) return;
      const d = JSON.parse(raw) as BulkDraft;
      if (!d || typeof d !== 'object') return;

      const rows = Array.isArray(d.rows)
        ? d.rows.map((r) => ({
            ...r,
            templateId: r.templateId ?? '',
            cvVersionId: r.cvVersionId ?? '',
            expanded: r.expanded === true,
            existingContact: r.existingContact === true,
            existingCompany: r.existingCompany === true,
            companySaved: r.companySaved === true,
            contactSaved: r.contactSaved === true,
          }))
        : [];
      const hasRows = rows.length > 0;
      const hasText = typeof d.pasteText === 'string' && d.pasteText.trim().length > 0;
      if (!hasRows && !hasText) return;

      this.pasteText = typeof d.pasteText === 'string' ? d.pasteText : '';
      this.rows.set(rows);
      this.keySeq = Number(d.keySeq) || rows.length;
      this.selectedTemplateId.set(d.selectedTemplateId ?? '');
      this.defaultTemplateId.set(d.defaultTemplateId ?? '');
      this.sharedVars.set(d.sharedVars && typeof d.sharedVars === 'object' ? d.sharedVars : {});
      this.manualSubject = d.manualSubject ?? '';
      this.manualBody = d.manualBody ?? '';
      this.selectedCvVersionId.set(d.selectedCvVersionId ?? '');
      this.delaySeconds = Number.isFinite(Number(d.delaySeconds)) ? Number(d.delaySeconds) : 5;
      this.composeMode.set(d.composeMode === 'manual' ? 'manual' : 'template');
      this.extractMethod.set(d.extractMethod ?? '');
      this.aiMapTemplates.set(d.aiMapTemplates === true);
      this.mapTemplateIds.set(Array.isArray(d.mapTemplateIds) ? d.mapTemplateIds : []);

      // Never restore the send/progress step — the run itself is server-side.
      const step = Number(d.step);
      const safeStep = step >= 1 && step <= 3 ? step : hasRows ? 3 : 1;
      this.step.set(safeStep as 1 | 2 | 3 | 4);

      this.draftRestored.set(true);
      this.draftSavedAt.set(Number(d.savedAt) || Date.now());
    } catch {
      /* corrupt/unavailable storage — ignore */
    }
  }

  schedulePersist() {
    if (!this.persistEnabled || this.draftSuppressed) return;
    if (this.persistHandle) clearTimeout(this.persistHandle);
    this.persistHandle = setTimeout(() => {
      this.persistHandle = null;
      this.persistDraft();
    }, DRAFT_DEBOUNCE_MS);
  }

  private persistDraft() {
    try {
      const hasContent =
        this.rows().length > 0 ||
        this.pasteText.trim().length > 0 ||
        this.manualSubject.trim().length > 0 ||
        this.manualBody.trim().length > 0;
      if (!hasContent) {
        localStorage.removeItem(DRAFT_KEY);
        this.draftSavedAt.set(null);
        return;
      }
      const payload: BulkDraft = {
        v: 1,
        savedAt: Date.now(),
        step: this.step(),
        pasteText: this.pasteText,
        rows: this.rows(),
        keySeq: this.keySeq,
        selectedTemplateId: this.selectedTemplateId(),
        defaultTemplateId: this.defaultTemplateId(),
        sharedVars: this.sharedVars(),
        manualSubject: this.manualSubject,
        manualBody: this.manualBody,
        selectedCvVersionId: this.selectedCvVersionId(),
        delaySeconds: Number(this.delaySeconds) || 0,
        composeMode: this.composeMode(),
        extractMethod: this.extractMethod(),
        aiMapTemplates: this.aiMapTemplates(),
        mapTemplateIds: this.mapTemplateIds(),
      };
      localStorage.setItem(DRAFT_KEY, JSON.stringify(payload));
      this.draftSavedAt.set(payload.savedAt);
    } catch {
      /* storage full/blocked — drafting is best-effort */
    }
  }

  private clearDraft() {
    if (this.persistHandle) {
      clearTimeout(this.persistHandle);
      this.persistHandle = null;
    }
    try {
      localStorage.removeItem(DRAFT_KEY);
    } catch {
      /* ignore */
    }
    this.draftRestored.set(false);
    this.draftSavedAt.set(null);
  }

  discardDraft() {
    this.clearDraft();
    this.toast.success('Draft discarded');
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

      const existing = await this.loadExistingEntities();
      this.keySeq = 0;
      this.rows.set(
        data.rows.map((r) => {
          const email = (r.email ?? '').trim();
          const isExisting = !!email && existing.emails.has(email.toLowerCase());
          // Missing company name: derive it from the email domain (test@companyname.com → "Companyname")
          const coName = (r.companyName ?? '').trim() || (email ? this.deriveCompanyFromEmail(email) : '');
          const isExistingCo = !!coName && existing.companyNames.has(coName.toLowerCase());
          return {
            key: `r${this.keySeq++}`,
            include: !isExisting,
            companyName: coName,
            location: r.location ?? '',
            specialty: (r.specialties ?? []).join(', '),
            email,
            contactName: r.contactName ?? '',
            subject: '',
            body: '',
            templateId: '',
            cvVersionId: '',
            expanded: false,
            existingContact: isExisting,
            existingCompany: isExistingCo,
            companySaved: false,
            contactSaved: false,
          };
        }),
      );
      this.extractMethod.set(data.method);

      // Optional: ask the AI to pick a template per row from the chosen subset, then resolve
      // each row's subject/body from that template. Rows the AI can't match stay blank.
      if (this.aiMapTemplates() && this.mapTemplateIds().length > 0) {
        await this.applyAiTemplateMapping();
      }

      this.step.set(2);
      const flags: string[] = [];
      if (existing.emails.size) flags.push(`${existing.emails.size} saved contact emails`);
      if (existing.companyNames.size) flags.push(`${existing.companyNames.size} saved companies`);
      if (flags.length) {
        this.toast.success(`Parsed ${data.rows.length} rows — existing ${flags.join(' and ')} were flagged`);
      }
    } catch (e: any) {
      this.error.set(e?.message || 'Extraction failed');
    } finally {
      this.extracting.set(false);
    }
  }

  /**
   * Ask the AI to pick, for each row, the best-fitting template from the user-chosen subset.
   * The returned template ids are then resolved client-side into subject/body (tokens filled
   * per row), so nothing is guessed when the AI returns no match — those rows stay blank.
   */
  private async applyAiTemplateMapping() {
    const chosenIds = new Set(this.mapTemplateIds());
    const candidates = this.templates().filter((t) => chosenIds.has(t.id));
    if (!candidates.length) return;

    try {
      const res = await this.bulkSvc.mapTemplates({
        rows: this.rows().map((r) => ({
          companyName: r.companyName,
          location: r.location,
          specialty: r.specialty,
        })),
        templates: candidates.map((t) => ({
          id: t.id,
          name: t.name,
          subjectTemplate: t.subjectTemplate,
          bodyTemplate: t.bodyTemplate,
          intent: t.subjectTemplate,
        })),
      });
      const ids = res.data?.templateIds;
      if (!res.success || !Array.isArray(ids)) return;

      this.rows.update((list) =>
        list.map((r, i) => {
          const tid = ids[i] ?? '';
          const t = tid ? selected(this.templates(), tid) : undefined;
          if (!t) return r;
          return {
            ...r,
            templateId: t.id,
            cvVersionId: t.cvVersionId ?? r.cvVersionId,
            subject: resolveTemplateVars(t.subjectTemplate, this.rowValues(r, t)),
            body: resolveTemplateVars(t.bodyTemplate, this.rowValues(r, t)),
          };
        }),
      );
      this.mappingMethod.set('ai');
      const matched = this.rows().filter((r) => r.templateId).length;
      this.toast.success(`AI mapped templates to ${matched} of ${this.rows().length} rows`);
    } catch {
      this.toast.error('AI template mapping failed — rows left blank for you to fill in');
    }
  }

  private async loadExistingEntities(): Promise<{ emails: Set<string>; companyNames: Set<string> }> {
    const emails = new Set<string>();
    const companyNames = new Set<string>();
    this.knownContactEmails.clear();
    this.knownCompanyNames.clear();
    try {
      const res = await this.contactSvc.getContacts({ page: 1, pageSize: 500 });
      for (const c of res.data?.items ?? []) {
        if (c.email) {
          emails.add(c.email.trim().toLowerCase());
        }
      }
    } catch {
      /* dedupe is best-effort */
    }
    try {
      const res = await this.companySvc.getCompanies({ page: 1, pageSize: 500 });
      for (const c of res.data?.items ?? []) {
        if (c.name) companyNames.add(c.name.trim().toLowerCase());
      }
    } catch {
      /* dedupe is best-effort */
    }
    for (const e of emails) this.knownContactEmails.add(e);
    for (const n of companyNames) this.knownCompanyNames.add(n);
    return { emails, companyNames };
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

  toggleExpand(key: string, ev?: Event) {
    let willExpand = false;
    this.rows.update((list) =>
      list.map((r) => {
        if (r.key === key) {
          willExpand = !r.expanded;
          return { ...r, expanded: willExpand };
        }
        return r;
      }),
    );
    if (willExpand && ev?.currentTarget) {
      const target = ev.currentTarget as HTMLElement;
      const item = target.closest('.row-item');
      if (item) {
        requestAnimationFrame(() => item.scrollIntoView({ block: 'nearest', behavior: 'smooth' }));
      }
    }
  }

  removeRow(key: string) {
    this.rows.update((list) => list.filter((r) => r.key !== key));
  }

  /**
   * Derives a company display name from an email domain when the name is missing:
   * test@companyname.com → "Companyname", hr@recruitment.capgemini.com → "Capgemini".
   * Mirrors BulkApplyService.DeriveCompanyName (backend authoritative at run/queue creation).
   */
  deriveCompanyFromEmail(email: string): string {
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

  /** When the company name cell is empty and the user types an email, auto-fill from the domain. */
  onEmailTyped(row: ReviewRow, value: string): void {
    row.email = value ?? '';
    if (!row.companyName.trim() && row.email.includes('@') && row.email.includes('.')) {
      row.companyName = this.deriveCompanyFromEmail(row.email);
    }
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
        templateId: '',
        cvVersionId: '',
        expanded: true,
        existingContact: false,
        existingCompany: false,
        companySaved: false,
        contactSaved: false,
      },
    ]);
  }

  // ── Step 2: save companies / contacts ──────────────────────────────────────────

  loadSample() {
    this.pasteText = buildSampleList(this.authSvc.currentUser()?.email ?? '');
    this.error.set('');
    this.toast.success('Sample list loaded — replace the aliases with your own inbox if needed');
  }

  // ── Step 1: AI template mapping controls ───────────────────────────────────────

  toggleAiMap(on: boolean) {
    this.aiMapTemplates.set(on);
    if (on && this.mapTemplateIds().length === 0) {
      // Default to all templates so the user can deselect the ones to ignore.
      this.mapTemplateIds.set(this.templates().map((t) => t.id));
    }
  }

  isTemplateChosenForMap(id: string): boolean {
    return this.mapTemplateIds().includes(id);
  }

  toggleMapTemplate(id: string) {
    this.mapTemplateIds.update((ids) =>
      ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id],
    );
  }

  chooseAllMapTemplates() {
    this.mapTemplateIds.set(this.templates().map((t) => t.id));
  }

  clearMapTemplates() {
    this.mapTemplateIds.set([]);
  }

  canSaveCompany(r: ReviewRow): boolean {
    return !!r.companyName.trim() && !r.existingCompany && !r.companySaved;
  }

  canSaveContact(r: ReviewRow): boolean {
    return !!r.email.trim() && !r.existingContact && !r.contactSaved;
  }

  saveableCompanies(): ReviewRow[] {
    return this.rows().filter((r) => this.canSaveCompany(r));
  }

  saveableContacts(): ReviewRow[] {
    return this.rows().filter((r) => this.canSaveContact(r));
  }

  async saveCompany(key: string): Promise<boolean> {
    const row = this.rows().find((r) => r.key === key);
    if (!row || !this.canSaveCompany(row)) return false;
    this.savingCompanyKey.set(key);
    try {
      const outcome = await this.postCompany(row);
      if (outcome === 'created') {
        this.markCompany(row.key, 'saved');
        return true;
      }
      if (outcome === 'exists') {
        this.markCompany(row.key, 'existing');
        return false;
      }
      this.toast.error(`Could not save ${row.companyName}`);
      return false;
    } finally {
      this.savingCompanyKey.set(null);
    }
  }

  async saveContact(key: string): Promise<boolean> {
    const row = this.rows().find((r) => r.key === key);
    if (!row || !this.canSaveContact(row)) return false;
    this.savingContactKey.set(key);
    try {
      const outcome = await this.postContact(row);
      if (outcome === 'created') {
        this.markContact(row.key, 'saved');
        return true;
      }
      if (outcome === 'exists') {
        this.markContact(row.key, 'existing');
        return false;
      }
      this.toast.error(`Could not save contact for ${row.companyName || row.email}`);
      return false;
    } finally {
      this.savingContactKey.set(null);
    }
  }

  async saveAllCompanies() {
    const targets = this.saveableCompanies();
    if (!targets.length) {
      this.toast.success('All companies with a name are already saved');
      return;
    }
    this.savingAllCompanies.set(true);
    let created = 0;
    let exists = 0;
    let failed = 0;
    try {
      for (const target of targets) {
        // Re-read the live row: earlier iterations may have resolved batch duplicates already.
        const row = this.rows().find((r) => r.key === target.key);
        if (!row || !this.canSaveCompany(row)) { exists++; continue; }
        const outcome = await this.postCompany(row);
        if (outcome === 'created') { created++; this.markCompany(row.key, 'saved'); }
        else if (outcome === 'exists') { exists++; this.markCompany(row.key, 'existing'); }
        else failed++;
      }
    } finally {
      this.savingAllCompanies.set(false);
    }
    const parts = [`${created} saved`];
    if (exists) parts.push(`${exists} already existed`);
    if (failed) parts.push(`${failed} failed`);
    const msg = `Companies: ${parts.join(' · ')}`;
    failed ? this.toast.error(msg) : this.toast.success(msg);
  }

  async saveAllContacts() {
    const targets = this.saveableContacts();
    const noEmail = this.rows().filter((r) => r.companyName.trim() && !r.email.trim() && !r.contactSaved).length;
    if (!targets.length) {
      this.toast.success('All contacts with an email are already saved');
      return;
    }
    this.savingAllContacts.set(true);
    let created = 0;
    let exists = 0;
    let failed = 0;
    try {
      for (const target of targets) {
        const row = this.rows().find((r) => r.key === target.key);
        if (!row || !this.canSaveContact(row)) { exists++; continue; }
        const outcome = await this.postContact(row);
        if (outcome === 'created') { created++; this.markContact(row.key, 'saved'); }
        else if (outcome === 'exists') { exists++; this.markContact(row.key, 'existing'); }
        else failed++;
      }
    } finally {
      this.savingAllContacts.set(false);
    }
    const parts = [`${created} saved`];
    if (exists) parts.push(`${exists} already existed`);
    if (noEmail) parts.push(`${noEmail} skipped (no email)`);
    if (failed) parts.push(`${failed} failed`);
    const msg = `Contacts: ${parts.join(' · ')}`;
    failed ? this.toast.error(msg) : this.toast.success(msg);
  }

  /** POST one company; 'created' | 'exists' (409 or name already known) | 'failed'. */
  private async postCompany(row: ReviewRow): Promise<'created' | 'exists' | 'failed'> {
    const name = row.companyName.trim();
    if (!name) return 'failed';
    const key = name.toLowerCase();
    if (this.knownCompanyNames.has(key)) return 'exists';
    try {
      const res = await this.companySvc.createCompany({
        name,
        location: row.location.trim() || undefined,
        specialties: row.specialty.trim() || undefined,
      });
      if (res.success) {
        this.knownCompanyNames.add(key);
        return 'created';
      }
      return res.message?.includes('already') ? 'exists' : 'failed';
    } catch (e) {
      // CompaniesController returns 409 Conflict on duplicate names.
      if (e instanceof HttpErrorResponse && e.status === 409) {
        this.knownCompanyNames.add(key);
        return 'exists';
      }
      return 'failed';
    }
  }

  /** POST one contact; 'created' | 'exists' | 'failed'. */
  private async postContact(row: ReviewRow): Promise<'created' | 'exists' | 'failed'> {
    const email = row.email.trim().toLowerCase();
    if (!email) return 'failed';
    if (this.knownContactEmails.has(email)) return 'exists';
    const name = (row.contactName.trim() || email.split('@')[0] || row.companyName.trim() || email).trim();
    try {
      const res = await this.contactSvc.createContact({
        name,
        email,
        company: row.companyName.trim() || undefined,
        source: 'bulk_apply',
      });
      if (res.success) {
        this.knownContactEmails.add(email);
        return 'created';
      }
      return res.message?.toLowerCase().includes('already') ? 'exists' : 'failed';
    } catch (e) {
      if (e instanceof HttpErrorResponse && e.status === 409) {
        this.knownContactEmails.add(email);
        return 'exists';
      }
      return 'failed';
    }
  }

  private markCompany(key: string, state: 'saved' | 'existing') {
    const row = this.rows().find((r) => r.key === key);
    if (!row) return;
    const norm = row.companyName.trim().toLowerCase();
    // Same-name rows (duplicates within the batch) share the outcome.
    this.rows.update((list) =>
      list.map((r) =>
        r.key === key || r.companyName.trim().toLowerCase() === norm
          ? {
              ...r,
              companySaved: r.companySaved || state === 'saved',
              existingCompany: r.existingCompany || state === 'existing',
            }
          : r,
      ),
    );
    if (state === 'saved') this.knownCompanyNames.add(norm);
  }

  private markContact(key: string, state: 'saved' | 'existing') {
    const row = this.rows().find((r) => r.key === key);
    if (!row) return;
    const email = row.email.trim().toLowerCase();
    if (email) this.knownContactEmails.add(email);
    this.rows.update((list) =>
      list.map((r) =>
        r.key === key || (email && r.email.trim().toLowerCase() === email)
          ? {
              ...r,
              contactSaved: r.contactSaved || state === 'saved',
              existingContact: r.existingContact || state === 'existing',
            }
          : r,
      ),
    );
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
    // Selecting a template for the whole batch also adopts its CV so the user doesn't have
    // to pick it twice; leave the batch CV untouched when the template has none.
    const t = selected(this.templates(), id);
    if (t?.cvVersionId) this.selectedCvVersionId.set(t.cvVersionId);
    this.applyTemplateToAll(true);
  }

  setSharedVar(token: string, value: string) {
    this.sharedVars.update((v) => ({ ...v, [token]: value }));
  }

  private rowValues(row: ReviewRow, template?: ScheduleTemplateDto | null): TemplateVarValues {
    const t = template ?? selected(this.templates(), this.selectedTemplateId());
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
              subject: resolveTemplateVars(t.subjectTemplate, this.rowValues(r, t)),
              body: resolveTemplateVars(t.bodyTemplate, this.rowValues(r, t)),
              templateId: t.id,
              cvVersionId: t.cvVersionId ?? r.cvVersionId,
            }
          : r,
      ),
    );
    this.error.set('');
    if (!silent) this.toast.success(`Template applied to ${this.includedRows().length} rows`);
  }

  /** Remember the selected template as the fallback for rows the AI left unassigned. */
  setDefaultTemplate() {
    const id = this.selectedTemplateId();
    if (!id) {
      this.error.set('Pick a template first.');
      return;
    }
    this.defaultTemplateId.set(id);
    this.error.set('');
    this.toast.success(`"${this.templateName(id)}" set as the default for unassigned rows`);
  }

  /** Apply the default template only to rows that have no template and no message yet. */
  applyDefaultToUnassigned() {
    const t = selected(this.templates(), this.defaultTemplateId());
    if (!t) {
      this.error.set('Set a default template first.');
      return;
    }
    let changed = 0;
    this.rows.update((list) =>
      list.map((r) => {
        if (!r.include || r.templateId || r.subject.trim() || r.body.trim()) return r;
        changed++;
        return {
          ...r,
          templateId: t.id,
          cvVersionId: t.cvVersionId ?? r.cvVersionId,
          subject: resolveTemplateVars(t.subjectTemplate, this.rowValues(r, t)),
          body: resolveTemplateVars(t.bodyTemplate, this.rowValues(r, t)),
        };
      }),
    );
    this.error.set('');
    this.toast.success(
      changed ? `Default applied to ${changed} unassigned row${changed === 1 ? '' : 's'}` : 'No unassigned rows to fill',
    );
  }

  unassignedCount(): number {
    return this.includedRows().filter((r) => !r.templateId && !r.subject.trim() && !r.body.trim()).length;
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
              templateId: '',
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

  /** Switch a single row's template (independent of the batch) and re-resolve its message. */
  setRowTemplate(key: string, templateId: string) {
    const t = templateId ? selected(this.templates(), templateId) : undefined;
    this.rows.update((list) =>
      list.map((r) => {
        if (r.key !== key) return r;
        if (!t) return { ...r, templateId: '' };
        return {
          ...r,
          templateId: t.id,
          cvVersionId: t.cvVersionId ?? r.cvVersionId,
          subject: resolveTemplateVars(t.subjectTemplate, this.rowValues(r, t)),
          body: resolveTemplateVars(t.bodyTemplate, this.rowValues(r, t)),
        };
      }),
    );
  }

  /** Set a single row's CV (overrides the batch CV for that row only). */
  setRowCv(key: string, cvVersionId: string) {
    this.rows.update((list) => list.map((r) => (r.key === key ? { ...r, cvVersionId } : r)));
  }

  /** Effectively the CV a row will use (its own, or the batch default). */
  effectiveRowCv(row: ReviewRow): string {
    return row.cvVersionId || this.selectedCvVersionId();
  }

  templateName(id: string): string {
    return selected(this.templates(), id)?.name ?? '';
  }

  /** Compact label for the row badge — truncates long template names. */
  templateShortName(id: string): string {
    const name = this.templateName(id);
    return name.length > 18 ? `${name.slice(0, 17)}…` : name;
  }

  /** Secondary line under the company name: specialty, falling back to location. */
  rowSubtitle(row: ReviewRow): string {
    const specialty = row.specialty.trim();
    if (specialty) return specialty;
    return row.location.trim();
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
      cvVersionId: r.cvVersionId || undefined,
    }));
    if (!rows.length) {
      this.error.set('No sendable rows.');
      return;
    }

    void this.createRun(rows);
  }

  // ── Save to stack (Apply Queue) ────────────────────────────────────────────────

  /** True while the current selection is being staged into the Apply Queue. */
  staging = signal(false);

  /**
   * Parks the sendable rows (companies/emails/subject/body/per-row CV) in the Apply Queue
   * instead of sending now. Each staged item also creates its SAVED tracking application
   * server-side, so the applies are visible in the kanban while they wait.
   */
  async saveToStack() {
    if (!this.allRowsHaveMessage()) {
      this.error.set('Every selected row needs a subject and body. Apply a template or write a message first.');
      return;
    }
    const rows = this.sendableRows().map((r) => ({
      companyName: r.companyName.trim(),
      location: r.location.trim() || undefined,
      positionTitle: r.specialty.trim() || undefined,
      recipientEmail: r.email.trim(),
      recipientName: r.contactName.trim() || undefined,
      subject: r.subject,
      body: r.body,
      cvVersionId: r.cvVersionId || undefined,
      source: 'bulk_apply' as const,
    }));
    if (!rows.length) {
      this.error.set('No sendable rows.');
      return;
    }
    this.starting.set(true);
    this.staging.set(true);
    try {
      const count = await this.queueSvc.saveBatch(rows);
      if (count === 0) {
        this.error.set('Could not stage the applies — try again');
        return;
      }
      this.toast.success(`Staged ${count} appl${count === 1 ? 'y' : 'ies'} in the Queue`);
      this.draftSuppressed = true;
      this.clearDraft();
      this.step.set(1);
      this.pasteText = '';
      this.error.set('');
      this.router.navigate(['/applications/queue']);
    } catch (e: any) {
      this.error.set(e?.message || 'Could not stage the applies');
    } finally {
      this.staging.set(false);
      this.starting.set(false);
    }
  }

  private async createRun(rows: {
    companyName: string;
    location?: string;
    specialty?: string;
    recipientEmail: string;
    recipientName?: string;
    subject: string;
    body: string;
    cvVersionId?: string;
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
      this.lastProcessed = -1;
      this.lastProgressAt = Date.now();
      this.stalled.set(false);
      // The batch is now tracked server-side — drop the local draft for good
      // (suppress re-saving, otherwise the step change below would rewrite it).
      this.draftSuppressed = true;
      this.clearDraft();
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
      if (res.success && res.data) {
        const next = res.data;
        // Stall detection: if ProcessedRows has not moved for ~45s while the run is still
        // live, surface an escape hatch instead of letting the UI look frozen forever.
        if (next.status === 'running' || next.status === 'pending') {
          if (next.processedRows !== this.lastProcessed) {
            this.lastProcessed = next.processedRows;
            this.lastProgressAt = Date.now();
            this.stalled.set(false);
          } else if (this.lastProgressAt > 0 && Date.now() - this.lastProgressAt > 45_000) {
            this.stalled.set(true);
          }
        }
        this.run.set(next);
      }
    } catch {
      /* transient poll failure — try again next tick */
    }
  }

  async cancelRun() {
    const id = this.runId();
    if (!id) return;
    this.cancelling.set(true);
    try {
      await this.bulkSvc.cancel(id);
      this.toast.success('Cancellation requested — stopping after the current row');
      // The backend now persists the flag; poll until it reaches a terminal state.
      await this.refreshRun();
      if (!this.runFinished()) this.schedulePoll();
    } catch (e: any) {
      this.toast.error(e?.message || 'Could not cancel');
    } finally {
      this.cancelling.set(false);
    }
  }

  async retryFailed() {
    const id = this.runId();
    if (!id) return;
    this.retrying.set(true);
    try {
      const res = await this.bulkSvc.retryFailed(id);
      if (!res.success) {
        this.toast.error(res.message || 'Could not retry the failed rows');
        return;
      }
      this.toast.success('Retrying failed rows…');
      this.lastProcessed = -1;
      this.lastProgressAt = Date.now();
      this.stalled.set(false);
      this.run.set(res.data ?? this.run());
      await this.refreshRun();
      if (!this.runFinished()) this.schedulePoll();
    } catch (e: any) {
      this.toast.error(e?.message || 'Could not retry the failed rows');
    } finally {
      this.retrying.set(false);
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
    this.draftSuppressed = false;
    this.clearDraft();
    this.run.set(null);
    this.runId.set(null);
    this.stalled.set(false);
    this.cancelling.set(false);
    this.lastProcessed = -1;
    this.lastProgressAt = 0;
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
