import { Component, signal, inject, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ApplicationService } from '@app/services/application.service';
import { ContactService } from '@app/services/contact.service';
import { AuthService } from '@app/services/auth.service';
import { CompanyService } from '@app/services/company.service';
import { DocumentsService } from '@app/services/documents.service';
import type { CompanyDto } from '@app/services/company.service';
import type { CvVersionDto } from '@app/models/document.model';
import {
  DuplicateMatchDto,
  ApiResponse,
  ApplicationResponseDto,
  ApplicationStatus,
  ApplicationPriority,
  AttemptChannel,
  AttemptInitiatedBy,
  ContactSummaryDto,
  attemptChannelFields,
  STATUS_LABELS,
  PRIORITY_LABELS,
  PRIORITY_ORDER,
} from '@app/models/application.model';

interface ChannelTile {
  value: AttemptChannel;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-application-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './application-create.component.html',
  styleUrl: './application-create.component.scss',
})
export class ApplicationCreateComponent implements OnInit {
  private appService = inject(ApplicationService);
  private contactApi = inject(ContactService);
  private companyApi = inject(CompanyService);
  private docsApi = inject(DocumentsService);
  private authService = inject(AuthService);
  protected router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  ngOnInit() {
    // Prefill support, e.g. /applications/new?companyName=… from a company detail page.
    const prefill = this.route.snapshot.queryParamMap.get('companyName');
    if (prefill) this.companyName.set(prefill);
    void this.loadCvVersions();
  }

  submitting = signal(false);
  error = signal<string | null>(null);
  duplicateWarning = signal<DuplicateMatchDto[] | null>(null);

  // ── Form state ──────────────────────────────────────────────────────────────
  companyName = signal('');
  positionTitle = signal('');
  offerSource = signal('');
  notes = signal('');
  internshipType = signal('');
  priority = signal<ApplicationPriority>('MEDIUM');

  /** Where the candidate stands at creation time. */
  stage = signal<'SAVED' | 'APPLIED'>('APPLIED');

  // ── First attempt (only used when stage === 'APPLIED') ──────────────────────
  channel = signal<AttemptChannel>('EMAIL_GMAIL');
  initiatedBy = signal<AttemptInitiatedBy>('USER');
  markSent = signal(true);
  subject = signal('');
  body = signal('');
  recipientName = signal('');
  recipientContact = signal('');

  /** Date of the first apply — defaults to today; backfill earlier applications by changing it. */
  appliedDate = signal(this.localTodayStr());

  // ── CV used for this application (defaults to the single active CV) ───────
  cvVersions = signal<{ version: CvVersionDto; label: string }[]>([]);
  cvVersionId = signal<string>('');
  cvLoading = signal(false);

  /** Label of the pre-selected active CV (used in the sidebar hint). */
  activeCvName = computed<string>(() => {
    const selected = this.cvVersionId();
    const hit = this.cvVersions().find(o => o.version.id === selected);
    return hit ? hit.label.replace(' (active)', '') : '';
  });

  /** Defaults the select to the active CV's latest version. */
  private async loadCvVersions() {
    if (this.cvLoading()) return;
    this.cvLoading.set(true);
    try {
      const res = await this.docsApi.listCvs();
      if (!res.success || !res.data) return;
      const active = res.data.find(c => c.isActive);
      const options: { version: CvVersionDto; label: string }[] = [];
      for (const cv of res.data) {
        const versions = [...cv.versions].sort((a, b) => b.versionNumber - a.versionNumber);
        versions.forEach((v, i) => {
          options.push({
            version: v,
            label: `${cv.title} · v${v.versionNumber}${i === 0 && cv.isActive ? ' (active)' : ''}${v.label ? ` — ${v.label}` : ''}`,
          });
        });
      }
      this.cvVersions.set(options);
      if (active && active.versions.length) {
        const latest = [...active.versions].sort((a, b) => b.versionNumber - a.versionNumber)[0];
        this.cvVersionId.set(latest.id);
      }
    } catch {
      // non-blocking — attempt still saves without a CV
    } finally {
      this.cvLoading.set(false);
    }
  }

  private localTodayStr(): string {
    const d = new Date();
    const off = d.getTimezoneOffset();
    return new Date(d.getTime() - off * 60000).toISOString().slice(0, 10);
  }

  private isoFromDate(value: string): string | undefined {
    if (!value) return undefined;
    const d = new Date(`${value}T00:00:00`);
    return Number.isNaN(d.getTime()) ? undefined : d.toISOString();
  }

  // Contact linking for the first attempt (search-all — no app id exists yet)
  pickedContact = signal<ContactSummaryDto | null>(null);
  contactSearch = signal('');
  contactResults = signal<ContactSummaryDto[]>([]);
  searchingContacts = signal(false);

  // Inline "add a new contact" (create + link in one go)
  newContactMode = signal(false);
  newContactName = signal('');
  newContactEmail = signal('');
  newContactCompany = signal('');
  newContactPosition = signal('');
  creatingContact = signal(false);
  newContactError = signal<string | null>(null);

  // ── Company name: suggest existing companies, allow a brand-new name ───────
  companyMatches = signal<CompanyDto[]>([]);
  searchingCompanies = signal(false);
  companySuggestOpen = signal(false);
  /** True when the typed name does NOT match an existing saved company. */
  companyIsNew = signal(false);
  /** The saved company matching the typed name (tracks its logo when it exists). */
  pickedCompany = signal<CompanyDto | null>(null);
  /** When the name is new, offer to persist it to the Companies directory. */
  saveCompanyInfo = signal(true);
  private companyTimer: ReturnType<typeof setTimeout> | null = null;

  /** Logo URL when the current company name resolves to a saved company that has one. */
  companyLogoUrl = computed<string | null>(() => this.pickedCompany()?.logoUrl ?? null);

  pickCompany(c: CompanyDto) {
    if (this.companyTimer) { clearTimeout(this.companyTimer); this.companyTimer = null; }
    this.companyName.set(c.name);
    this.companyIsNew.set(false);
    this.companySuggestOpen.set(false);
    this.companyMatches.set([]);
    this.pickedCompany.set(c);
  }

  onCompanyInput(v: string) {
    this.companyName.set(v);
    const trimmed = v.trim();
    this.companyIsNew.set(trimmed.length > 0);
    // A live edit breaks the logo match — clear it until a suggestion is re-picked.
    const picked = this.pickedCompany();
    if (picked && picked.name.toLowerCase() !== trimmed.toLowerCase()) this.pickedCompany.set(null);
    if (!trimmed) {
      this.companyMatches.set([]);
      this.companySuggestOpen.set(false);
      this.pickedCompany.set(null);
      return;
    }
    this.companySuggestOpen.set(true);
    if (this.companyTimer) clearTimeout(this.companyTimer);
    this.companyTimer = setTimeout(() => this.loadCompanySuggestions(trimmed), 220);
  }

  onCompanyFocus() {
    if (this.companyName().trim()) {
      this.companySuggestOpen.set(true);
      this.searchingCompanies.set(true);
      this.companyApi.getCompanies({ search: this.companyName().trim(), pageSize: 8, sortBy: 'name' })
        .then(res => { if (res.success && res.data) this.companyMatches.set(res.data.items); })
        .catch(() => { })
        .finally(() => this.searchingCompanies.set(false));
    }
  }

  private loadCompanySuggestions(term: string) {
    this.searchingCompanies.set(true);
    this.companyApi.getCompanies({ search: term, pageSize: 8, sortBy: 'name' })
      .then(res => {
        const items = res.success && res.data ? res.data.items : [];
        this.companyMatches.set(items);
        const cur = this.companyName().trim().toLowerCase();
        this.companyIsNew.set(cur.length > 0 && !items.some(c => c.name.toLowerCase() === cur));
        // Auto-resolve the logo when the typed name is an exact match.
        const exact = items.find(c => c.name.toLowerCase() === cur);
        if (exact) this.pickedCompany.set(exact);
      })
      .catch(() => {
        this.companyMatches.set([]);
        this.companyIsNew.set(this.companyName().trim().length > 0);
      })
      .finally(() => this.searchingCompanies.set(false));
  }

  searchContacts() {
    const term = this.contactSearch().trim();
    if (term.length < 2) { this.contactResults.set([]); return; }
    this.searchingContacts.set(true);
    this.contactApi.getContacts({ search: term, pageSize: 6 })
      .then(res => {
        if (res.success && res.data) {
          this.contactResults.set(res.data.items.map(c => ({
            id: c.id, name: c.name, email: c.email,
            company: c.company, position: c.position, isFavorite: c.isFavorite,
          })));
        }
      })
      .catch(() => { })
      .finally(() => this.searchingContacts.set(false));
  }

  pickContact(c: ContactSummaryDto) {
    this.pickedContact.set(c);
    const cfg = this.channelFields();
    if (cfg.recipientName && !this.recipientName().trim()) this.recipientName.set(c.name);
    const ch = this.channel();
    if (cfg.recipientContact && !this.recipientContact().trim() && (ch === 'EMAIL_GMAIL' || ch === 'EMAIL_SMTP')) {
      this.recipientContact.set(c.email);
    }
    this.contactSearch.set('');
    this.contactResults.set([]);
  }

  openNewContact() {
    this.newContactMode.set(true);
    const term = this.contactSearch().trim();
    if (term && !this.newContactName().trim()) this.newContactName.set(term);
    if (!this.newContactCompany().trim()) this.newContactCompany.set(this.companyName().trim());
    this.newContactError.set(null);
  }

  closeNewContact() { this.newContactMode.set(false); this.newContactError.set(null); }

  async saveNewContact() {
    const name = this.newContactName().trim();
    const email = this.newContactEmail().trim();
    if (!name || !email) { this.newContactError.set('Name and email are required'); return; }
    this.creatingContact.set(true);
    this.newContactError.set(null);
    try {
      const res = await this.contactApi.createContact({
        name,
        email,
        company: this.newContactCompany().trim() || undefined,
        position: this.newContactPosition().trim() || undefined,
      });
      if (!res.success || !res.data) {
        this.newContactError.set(res.message || 'Failed to create contact');
        return;
      }
      const c = res.data;
      this.pickContact({ id: c.id, name: c.name, email: c.email, company: c.company, position: c.position, isFavorite: c.isFavorite });
      this.newContactMode.set(false);
    } catch {
      this.newContactError.set('Failed to create contact');
    } finally {
      this.creatingContact.set(false);
    }
  }

  clearPickedContact() { this.pickedContact.set(null); }

  protected readonly STATUS_LABELS = STATUS_LABELS;
  protected readonly PRIORITY_LABELS = PRIORITY_LABELS;
  protected readonly PRIORITY_ORDER = PRIORITY_ORDER;

  readonly channels: ChannelTile[] = [
    { value: 'EMAIL_GMAIL',          label: 'Gmail',              icon: 'ti-brand-gmail' },
    { value: 'EMAIL_SMTP',           label: 'Email (SMTP)',       icon: 'ti-mail' },
    { value: 'WHATSAPP',             label: 'WhatsApp',           icon: 'ti-brand-whatsapp' },
    { value: 'LINKEDIN_MESSAGE',     label: 'LinkedIn message',   icon: 'ti-brand-linkedin' },
    { value: 'LINKEDIN_CONNECTION',  label: 'LinkedIn connect',   icon: 'ti-user-plus' },
    { value: 'LINKEDIN_APPLY',       label: 'LinkedIn apply',     icon: 'ti-brand-linkedin' },
    { value: 'WEB_FORM',             label: 'Web form',           icon: 'ti-world' },
    { value: 'IN_PERSON',            label: 'In person',          icon: 'ti-users-group' },
    { value: 'OTHER',                label: 'Other',              icon: 'ti-dots' },
  ];

  protected readonly INITIATORS: { value: AttemptInitiatedBy; label: string }[] = [
    { value: 'USER', label: 'Me' },
    { value: 'AI_AGENT', label: 'AI agent' },
    { value: 'SCHEDULE', label: 'Scheduled' },
  ];

  /** Which attempt fields apply per channel (email gets subject, web form gets the URL, …). */
  readonly channelFields = computed(() => attemptChannelFields(this.channel()));

  get isFormValid(): boolean {
    return this.companyName().trim().length > 0 && this.positionTitle().trim().length > 0;
  }

  setStage(stage: 'SAVED' | 'APPLIED') { this.stage.set(stage); }
  dismissDuplicates() { this.duplicateWarning.set(null); }

  // ── Live preview card ───────────────────────────────────────────────────────
  previewStatus = computed<ApplicationStatus>(() => (this.stage() === 'APPLIED' ? 'APPLIED' : 'SAVED'));
  previewDate = computed(() => {
    if (this.stage() !== 'APPLIED') return null;
    const iso = this.isoFromDate(this.appliedDate());
    return iso ? new Date(iso) : new Date();
  });

  async onSubmit(force = false) {
    if (!this.isFormValid || this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);

    const user = this.authService.currentUser();
    if (!user) {
      this.error.set('You must be logged in');
      this.submitting.set(false);
      return;
    }

    try {
      // Pre-flight duplicate check unless the user already chose to force-create.
      if (!force) {
        try {
          const dupRes = await this.appService.checkDuplicate({
            companyName: this.companyName().trim(),
            positionTitle: this.positionTitle().trim(),
          });
          if (dupRes.success && dupRes.data?.hasDuplicates) {
            this.duplicateWarning.set(dupRes.data.matches);
            this.submitting.set(false);
            return;
          }
        } catch { /* check failed — let create decide */ }
      }

      // If the company name is brand new and the user opted in, save it to the directory.
      if (this.companyIsNew() && this.saveCompanyInfo()) {
        this.companyApi.createCompany({ name: this.companyName().trim() }).catch(() => { /* non-blocking */ });
      }

      const res = await this.appService.create({
        candidateId: user.userId,
        companyName: this.companyName().trim(),
        positionTitle: this.positionTitle().trim(),
        offerSource: this.offerSource().trim() || undefined,
        notes: this.notes().trim() || undefined,
        status: this.stage(),
        allowDuplicate: force || undefined,
        internshipType: this.internshipType().trim() || undefined,
        priority: this.priority(),
        appliedAt: this.stage() === 'APPLIED' ? this.isoFromDate(this.appliedDate()) : undefined,
      });

      if (!res.success || !res.data) {
        this.error.set(res.message || 'Failed to create');
        return;
      }

      const appId = res.data.id;

      // Log the first apply attempt — a SENT attempt flips the app to APPLIED server-side.
      if (this.stage() === 'APPLIED') {
        const cfg = this.channelFields();
        try {
          await this.appService.createAttempt(appId, {
            channel: this.channel(),
            initiatedBy: this.initiatedBy(),
            status: this.markSent() ? 'SENT' : 'DRAFT',
            subject: cfg.subject ? (this.subject().trim() || undefined) : undefined,
            body: cfg.message ? (this.body().trim() || undefined) : undefined,
            recipientName: cfg.recipientName ? (this.recipientName().trim() || undefined) : undefined,
            recipientContact: cfg.recipientContact ? (this.recipientContact().trim() || undefined) : undefined,
            contactId: this.pickedContact()?.id,
            channelMetadataJson: ((this.channel() === 'WEB_FORM' || this.channel() === 'LINKEDIN_APPLY') && this.recipientContact().trim())
              ? JSON.stringify({ formUrl: this.recipientContact().trim() })
              : undefined,
            cvVersionId: this.cvVersionId() || undefined,
            sentAt: this.markSent() ? (this.isoFromDate(this.appliedDate()) ?? new Date().toISOString()) : undefined,
          });
        } catch { /* attempt logging failed — app still created */ }
      }

      this.router.navigate(['/applications', appId]);
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 409) {
        const apiErr = err.error as ApiResponse<ApplicationResponseDto>;
        const payload = apiErr?.errors as { matches?: DuplicateMatchDto[] } | undefined;
        if (payload?.matches?.length) {
          this.duplicateWarning.set(payload.matches);
        } else {
          this.error.set(apiErr?.message || 'This application already exists');
        }
      } else {
        this.error.set(err instanceof HttpErrorResponse
          ? ((err.error as ApiResponse<unknown>)?.message ?? 'An error occurred')
          : err instanceof Error ? err.message : 'An error occurred');
      }
    } finally {
      this.submitting.set(false);
    }
  }

  formatPreviewDate(d: Date | null): string {
    return d ? d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' }) : '—';
  }

  statusLabel(s: string): string {
    return STATUS_LABELS[s as ApplicationStatus] ?? s;
  }
}
