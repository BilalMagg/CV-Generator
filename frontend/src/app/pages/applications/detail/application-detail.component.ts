import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApplicationService } from '@app/services/application.service';
import { ContactService } from '@app/services/contact.service';
import {
  ApplicationResponseDto,
  ApplicationStatus,
  ApplicationPriority,
  AttemptResponseDto,
  AttemptChannel,
  AttemptInitiatedBy,
  AttemptStatus,
  ContactSummaryDto,
  STATUS_LABELS,
  STATUS_COLORS,
  NEXT_STATUSES,
  PRIORITY_LABELS,
  PRIORITY_ORDER,
  PRIORITY_COLORS,
  attemptChannelFields,
  ATTEMPT_CHANNEL_LABELS,
  ATTEMPT_STATUS_LABELS,
} from '@app/models/application.model';
import { ContactDto, EmailMessageDto, ScheduleAttachmentRef } from '@app/models/mailbox.model';
import { MailboxService } from '@app/services/mailbox.service';
import { CoverLetterDto, CoverLetterVersionDto, CvDocumentDto, CvVersionDto } from '@app/models/document.model';
import { DocumentsService } from '@app/services/documents.service';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';
import { ConfirmService } from '@app/services/confirm.service';

type AttemptForm = {
  channel: AttemptChannel;
  initiatedBy: AttemptInitiatedBy;
  subject: string;
  body: string;
  recipientName: string;
  recipientContact: string;
  coverLetterVersionId: string | null;
};

@Component({
  selector: 'app-application-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, RefreshButtonComponent],
  templateUrl: './application-detail.component.html',
  styleUrl: './application-detail.component.scss',
})
export class ApplicationDetailComponent implements OnInit {
  private appService = inject(ApplicationService);
  private confirm = inject(ConfirmService);
  private contactApi = inject(ContactService);
  private documents = inject(DocumentsService);
  private mailbox = inject(MailboxService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  application = signal<ApplicationResponseDto | null>(null);
  attempts = signal<AttemptResponseDto[]>([]);
  loading = signal(true);
  refreshing = signal(false);
  saving = signal(false);
  error = signal<string | null>(null);
  showStatusModal = signal(false);
  showEditModal = signal(false);
  showAttemptModal = signal(false);

  newStatus = signal<ApplicationStatus>('APPLIED');
  statusComment = signal('');
  editCompanyName = signal('');
  editPositionTitle = signal('');
  editOfferSource = signal('');
  editNotes = signal('');
  editInternshipType = signal('');
  editPriority = signal<ApplicationPriority>('MEDIUM');

  attemptForm = signal<AttemptForm>({
    channel: 'EMAIL_GMAIL',
    initiatedBy: 'USER',
    subject: '',
    body: '',
    recipientName: '',
    recipientContact: '',
    coverLetterVersionId: null,
  });
  attemptError = signal<string | null>(null);

  // Cover letter picker (attempt modal)
  coverLetters = signal<CoverLetterDto[]>([]);
  coverLetterOptions = computed(() =>
    this.coverLetters().flatMap(l =>
      l.versions.map(v => ({
        versionId: v.id,
        label: `${l.title} — v${v.versionNumber}`,
        company: l.companyName || 'General',
      })),
    ),
  );

  // Contact picker (attempt modal)
  suggestedContacts = signal<ContactSummaryDto[]>([]);
  selectedContactId = signal<string | null>(null);
  contactSearch = signal('');
  searchResults = signal<ContactSummaryDto[]>([]);
  searching = signal(false);
  showInlineCreate = signal(false);
  inlineName = signal('');
  inlineEmail = signal('');
  inlineCompany = signal('');
  inlineError = signal<string | null>(null);

  // Contact side panel
  panelOpen = signal(false);
  panelLoading = signal(false);
  panelContact = signal<ContactDto | null>(null);
  panelEmails = signal<EmailMessageDto[]>([]);
  panelApplications = signal<ApplicationResponseDto[]>([]);

  availableStatuses = computed(() => NEXT_STATUSES[this.application()?.status || 'SAVED'] || []);

  // Application attachments (right-column card)
  appAttachments = computed<ScheduleAttachmentRef[]>(() => this.application()?.attachments ?? []);
  attachmentsUploading = signal(false);
  attachmentError = signal<string | null>(null);

  // CV sent with this apply (optional, from the user's Documents CVs)
  cvDocuments = signal<CvDocumentDto[]>([]);
  docLoading = signal(false);
  private docLoaded = false;
  cvOptions = computed(() =>
    this.cvDocuments().flatMap(cv =>
      cv.versions.map(v => ({
        versionId: v.id,
        label: `${cv.title} — v${v.versionNumber}${v.label ? ` · ${v.label}` : ''}`,
      })),
    ),
  );
  selectedCvValue = computed(() => this.application()?.cvVersionId ?? '');

  // From-Documents picker modal
  docsOpen = signal(false);
  docLetters = signal<CoverLetterDto[]>([]);

  // Follow-up reminder (right-column card): presets relative to the applied date,
  // custom date, or cleared. A set deadline becomes a PENDING reminder on the Calendar.
  followUpPresets = [
    { label: '3 days', days: 3 },
    { label: '1 week', days: 7 },
    { label: '2 weeks', days: 14 },
    { label: '1 month', days: 30 },
  ];
  followUpSaving = signal(false);
  followUpError = signal<string | null>(null);
  customFollowUpDate = signal('');

  followUpDeadline = computed(() => this.application()?.followUpDate ?? null);
  followUpStatusLabel = computed(() => {
    const s = this.application()?.followUpStatus;
    return s === 'PENDING' ? 'Pending reply' : s === 'ACTIONED' ? 'Actioned' : 'Not set';
  });
  followUpOverdue = computed(() => {
    const d = this.followUpDeadline();
    if (!d) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return new Date(d) < today;
  });

  /** Target date-input value for a preset: applied date + offset days (falls back to today). */
  presetTarget(days: number): string {
    const base = this.application()?.appliedAt ? new Date(this.application()!.appliedAt!) : new Date();
    const d = new Date(base.getFullYear(), base.getMonth(), base.getDate() + days);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  }

  async setFollowUp(dateInput: string) {
    const app = this.application();
    if (!app || this.followUpSaving()) return;
    this.followUpSaving.set(true);
    this.followUpError.set(null);
    try {
      const res = await this.appService.update(app.id, {
        followUpDate: dateInput ? new Date(dateInput + 'T12:00:00').toISOString() : undefined,
        clearFollowUp: !dateInput,
      });
      if (res.success && res.data) this.application.set(res.data);
      else this.followUpError.set(res.message || 'Failed to save follow-up');
    } catch {
      this.followUpError.set('Failed to save follow-up');
    } finally {
      this.followUpSaving.set(false);
    }
  }

  sortedAttempts = computed(() =>
    [...this.attempts()].sort((a, b) => b.attemptNumber - a.attemptNumber)
  );

  protected readonly STATUS_LABELS = STATUS_LABELS;
  protected readonly PRIORITY_LABELS = PRIORITY_LABELS;
  protected readonly PRIORITY_ORDER = PRIORITY_ORDER;
  protected readonly CHANNEL_LABELS = ATTEMPT_CHANNEL_LABELS;
  protected readonly ATTEMPT_STATUS_LABELS = ATTEMPT_STATUS_LABELS;
  protected readonly ALL_CHANNELS: AttemptChannel[] =
    Object.keys(ATTEMPT_CHANNEL_LABELS) as AttemptChannel[];
  protected readonly INITIATORS: AttemptInitiatedBy[] = ['USER', 'AI_AGENT', 'SCHEDULE'];
  protected readonly channelFields = computed(() => attemptChannelFields(this.attemptForm().channel));

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadApplication(id);
    void this.ensureDocumentsLoaded();
  }

  async loadApplication(id: string) {
    this.loading.set(true);
    try {
      const res = await this.appService.getById(id);
      if (res.success && res.data) {
        this.application.set(res.data);
        this.resetEditForm(res.data);
      } else {
        this.error.set(res.message || 'Failed to load');
      }
      const attRes = await this.appService.getAttempts(id);
      if (attRes.success && attRes.data) this.attempts.set(attRes.data);
    } catch { this.error.set('Failed to load'); }
    finally { this.loading.set(false); this.refreshing.set(false); }
  }

  onRefresh() {
    this.refreshing.set(true);
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadApplication(id);
  }

  resetEditForm(app: ApplicationResponseDto) {
    this.editCompanyName.set(app.companyName);
    this.editPositionTitle.set(app.positionTitle);
    this.editOfferSource.set(app.offerSource || '');
    this.editNotes.set(app.notes || '');
    this.editInternshipType.set(app.internshipType || '');
    this.editPriority.set(app.priority || 'MEDIUM');
  }

  openStatusModal() {
    this.newStatus.set(this.availableStatuses()[0] || 'APPLIED');
    this.statusComment.set('');
    this.showStatusModal.set(true);
  }
  closeStatusModal() { this.showStatusModal.set(false); }
  openEditModal() { const app = this.application(); if (app) this.resetEditForm(app); this.showEditModal.set(true); }
  closeEditModal() { this.showEditModal.set(false); }

  openAttemptModal() {
    const app = this.application();
    this.attemptForm.set({
      channel: 'EMAIL_GMAIL',
      initiatedBy: 'USER',
      subject: app ? `Application – ${app.positionTitle}` : '',
      body: '',
      recipientName: '',
      recipientContact: '',
      coverLetterVersionId: null,
    });
    this.attemptError.set(null);
    this.showAttemptModal.set(true);
    // Load company-matching suggestions for quick recipient selection.
    if (app) {
      this.appService.getSuggestedContacts(app.id)
        .then(res => { if (res.success && res.data) this.suggestedContacts.set(res.data); })
        .catch(() => { });
    }
    this.loadCoverLetters();
  }
  closeAttemptModal() {
    this.showAttemptModal.set(false);
    this.resetPicker();
  }

  onAttemptChannelChange(ch: AttemptChannel) {
    // Replace the form object so `channelFields` (a computed on attemptForm) re-evaluates.
    this.attemptForm.update(f => ({ ...f, channel: ch }));
  }

  async loadCoverLetters(): Promise<void> {
    try {
      const res = await this.documents.listCoverLetters();
      this.coverLetters.set(res.data ?? []);
    } catch {
      this.coverLetters.set([]);
    }
  }

  private resetPicker() {
    this.selectedContactId.set(null);
    this.contactSearch.set('');
    this.searchResults.set([]);
    this.showInlineCreate.set(false);
    this.inlineName.set('');
    this.inlineEmail.set('');
    this.inlineCompany.set('');
    this.inlineError.set(null);
  }

  pickContact(c: ContactSummaryDto) {
    this.selectedContactId.set(c.id);
    const cfg = this.channelFields();
    const ch = this.attemptForm().channel;
    this.attemptForm.update(f => ({
      ...f,
      recipientName: cfg.recipientName ? (f.recipientName.trim() || c.name) : f.recipientName,
      recipientContact: (cfg.recipientContact && (ch === 'EMAIL_GMAIL' || ch === 'EMAIL_SMTP'))
        ? c.email : f.recipientContact,
    }));
    this.contactSearch.set('');
    this.searchResults.set([]);
    this.showInlineCreate.set(false);
  }

  clearContact() {
    this.selectedContactId.set(null);
  }

  async searchAllContacts() {
    const term = this.contactSearch().trim();
    if (term.length < 2) { this.searchResults.set([]); return; }
    this.searching.set(true);
    try {
      const res = await this.contactApi.getContacts({ search: term, pageSize: 8 });
      if (res.success && res.data) {
        this.searchResults.set(res.data.items.map(c => ({
          id: c.id, name: c.name, email: c.email,
          company: c.company, position: c.position, isFavorite: c.isFavorite,
        })));
      }
    } catch { }
    finally { this.searching.set(false); }
  }

  toggleInlineCreate() {
    this.showInlineCreate.update(v => !v);
    this.inlineError.set(null);
  }

  async createInlineContact() {
    const name = this.inlineName().trim();
    const email = this.inlineEmail().trim();
    this.inlineError.set(null);
    if (!name || !email) { this.inlineError.set('Name and email are required'); return; }
    try {
      const res = await this.contactApi.createContact({
        name, email,
        company: this.inlineCompany().trim() || undefined,
      });
      if (res.success && res.data) {
        this.pickContact({
          id: res.data.id, name: res.data.name, email: res.data.email,
          company: res.data.company, position: res.data.position, isFavorite: res.data.isFavorite,
        });
        this.suggestedContacts.update(list => list.some(c => c.id === res.data!.id)
          ? list
          : [...list, { id: res.data!.id, name: res.data!.name, email: res.data!.email, company: res.data!.company, position: res.data!.position, isFavorite: res.data!.isFavorite }]);
      } else {
        this.inlineError.set(res.message || 'Failed to create contact');
      }
    } catch (e: unknown) {
      const err = e as { error?: { message?: string }; message?: string };
      this.inlineError.set(err?.error?.message || err?.message || 'Failed to create contact');
    }
  }

  selectedContact = computed(() => {
    const id = this.selectedContactId();
    if (!id) return null;
    return this.suggestedContacts().find(c => c.id === id) ?? null;
  });

  async openContactPanel(attempt: AttemptResponseDto) {
    if (!attempt.contactId) return;
    this.panelOpen.set(true);
    this.panelLoading.set(true);
    this.panelContact.set(null);
    this.panelEmails.set([]);
    this.panelApplications.set([]);
    try {
      const [histRes, appsRes] = await Promise.all([
        this.contactApi.getContactHistory(attempt.contactId),
        this.appService.getApplicationsForContact(attempt.contactId).catch(() => null),
      ]);
      if (histRes.success && histRes.data) {
        this.panelContact.set(histRes.data.contact);
        this.panelEmails.set(histRes.data.emails || []);
      }
      if (appsRes?.success && appsRes.data) {
        this.panelApplications.set(appsRes.data.filter((a): a is ApplicationResponseDto => !!a));
      }
    } catch { }
    finally { this.panelLoading.set(false); }
  }

  closeContactPanel() { this.panelOpen.set(false); }

  async createAttempt(markSent: boolean) {
    const app = this.application();
    if (!app) return;
    const f = this.attemptForm();
    const cfg = this.channelFields();
    this.saving.set(true);
    this.attemptError.set(null);
    try {
      const res = await this.appService.createAttempt(app.id, {
        channel: f.channel,
        initiatedBy: f.initiatedBy,
        status: markSent ? 'SENT' : 'DRAFT',
        subject: cfg.subject ? (f.subject.trim() || undefined) : undefined,
        body: cfg.message ? (f.body.trim() || undefined) : undefined,
        recipientName: cfg.recipientName ? (f.recipientName.trim() || undefined) : undefined,
        recipientContact: cfg.recipientContact ? (f.recipientContact.trim() || undefined) : undefined,
        contactId: this.selectedContactId() ?? undefined,
        channelMetadataJson: (f.channel === 'WEB_FORM' && f.recipientContact.trim())
          ? JSON.stringify({ formUrl: f.recipientContact.trim() })
          : undefined,
        coverLetterVersionId: f.coverLetterVersionId ?? null,
        sentAt: markSent ? new Date().toISOString() : undefined,
      });
      if (res.success && res.data) {
        this.closeAttemptModal();
        await this.loadApplication(app.id);
      } else {
        this.attemptError.set(res.message || 'Failed to log attempt');
      }
    } catch { this.attemptError.set('Failed to log attempt'); }
    finally { this.saving.set(false); }
  }

  async setAttemptStatus(attempt: AttemptResponseDto, status: AttemptStatus, failureReason?: string) {
    const app = this.application();
    if (!app) return;
    try {
      await this.appService.updateAttempt(app.id, attempt.id, {
        status,
        failureReason,
        ...(status === 'SENT' && !attempt.sentAt ? { sentAt: new Date().toISOString() } : {}),
      });
      await this.loadApplication(app.id);
    } catch { }
  }

  async updateStatus() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.updateStatus(app.id, { status: this.newStatus(), comment: this.statusComment() || undefined });
      if (res.success && res.data) this.application.set(res.data);
      this.closeStatusModal();
    } catch { } finally { this.saving.set(false); }
  }

  async saveEdit() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.update(app.id, {
        companyName: this.editCompanyName(), positionTitle: this.editPositionTitle(),
        offerSource: this.editOfferSource() || undefined, notes: this.editNotes() || undefined,
        internshipType: this.editInternshipType().trim() || undefined,
        priority: this.editPriority(),
      });
      if (res.success && res.data) this.application.set(res.data);
      this.closeEditModal();
    } catch { } finally { this.saving.set(false); }
  }

  async onDelete() {
    const app = this.application();
    if (!app || !(await this.confirm.confirm({ message: 'Delete this application?', variant: 'danger' }))) return;
    try { await this.appService.delete(app.id); this.router.navigate(['/applications/kanban']); }
    catch { }
  }

  getStatusLabel(s: string) { return STATUS_LABELS[s as ApplicationStatus] || s; }

  formatDate(d?: string | null) {
    if (!d) return '—';
    return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });
  }

  formatDateTime(d?: string | null) {
    if (!d) return '—';
    return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  statusColor(s: string): string {
    return STATUS_COLORS[s as ApplicationStatus] ?? STATUS_COLORS.SAVED;
  }

  priorityColor(p?: string): string {
    return PRIORITY_COLORS[p as ApplicationPriority] ?? PRIORITY_COLORS.MEDIUM;
  }

  attachmentDownloadUrl(index: number): string {
    const app = this.application();
    return app ? `/api/applications/${app.id}/attachments/${index}/download` : '';
  }

  async onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (files.length > 0) await this.uploadAttachments(files);
  }

  async uploadAttachments(files: File[]) {
    const app = this.application();
    if (!app || this.attachmentsUploading()) return;
    this.attachmentsUploading.set(true);
    this.attachmentError.set(null);
    try {
      const refs = await Promise.all(files.map(f => this.mailbox.uploadAttachment(f)));
      const failed = refs.find(r => !r.success || !r.data);
      if (failed) { this.attachmentError.set(failed.message || 'Upload failed'); return; }
      const next = [...(app.attachments ?? []), ...refs.map(r => r.data!)];
      const res = await this.appService.update(app.id, { attachments: next });
      if (res.success && res.data) {
        this.application.set(res.data);
      } else {
        this.attachmentError.set(res.message || 'Failed to save attachments');
      }
    } catch {
      this.attachmentError.set('Upload failed');
    } finally {
      this.attachmentsUploading.set(false);
    }
  }

  async removeAttachment(index: number) {
    const app = this.application();
    if (!app || this.attachmentsUploading()) return;
    this.attachmentError.set(null);
    try {
      const next = (app.attachments ?? []).filter((_, i) => i !== index);
      const res = await this.appService.update(app.id, { attachments: next });
      if (res.success && res.data) {
        this.application.set(res.data);
      } else {
        this.attachmentError.set(res.message || 'Failed to remove attachment');
      }
    } catch {
      this.attachmentError.set('Failed to remove attachment');
    }
  }

  formatBytes(bytes?: number): string {
    if (!bytes || bytes <= 0) return '';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  // ── CV sent with this apply ─────────────────────────────────────────────
  async ensureDocumentsLoaded() {
    if (this.docLoaded || this.docLoading()) return;
    this.docLoading.set(true);
    try {
      const cvs = await this.documents.listCvs();
      if (cvs.success && cvs.data) this.cvDocuments.set(cvs.data);
      const letters = await this.documents.listCoverLetters();
      if (letters.success && letters.data) this.docLetters.set(letters.data);
      this.docLoaded = true;
    } catch {
    } finally {
      this.docLoading.set(false);
    }
  }

  /** Updates the application's CV version. '' clears it (sentinel Guid.Empty). */
  async setApplicationCv(value: string) {
    const app = this.application();
    if (!app) return;
    this.attachmentError.set(null);
    try {
      const res = await this.appService.update(app.id, {
        cvVersionId: value ? value : '00000000-0000-0000-0000-000000000000',
      });
      if (res.success && res.data) {
        this.application.set(res.data);
      } else {
        this.attachmentError.set(res.message || 'Failed to update CV');
      }
    } catch {
      this.attachmentError.set('Failed to update CV');
    }
  }

  // ── From-Documents picker ───────────────────────────────────────────────
  openDocsPicker() {
    void this.ensureDocumentsLoaded();
    this.docsOpen.set(true);
  }
  closeDocsPicker() { this.docsOpen.set(false); }

  docVersionLabel(v: CvVersionDto): string {
    const base = `v${v.versionNumber}`;
    return v.label ? `${base} · ${v.label}` : base;
  }

  async pickDocVersion(cv: CvDocumentDto, v: CvVersionDto) {
    const name = `${cv.title} — ${this.docVersionLabel(v)}.pdf`;
    const ok = await this.attachBlobAsFile(name, () => this.documents.getVersionFileBlob(v.id));
    if (ok) this.closeDocsPicker();
  }

  async pickLetterVersion(letter: CoverLetterDto, v: CoverLetterVersionDto) {
    const name = `${letter.title}${letter.companyName ? ` (${letter.companyName})` : ''} — v${v.versionNumber}.pdf`;
    const ok = await this.attachBlobAsFile(name, () => this.documents.getCoverLetterVersionFileBlob(v.id));
    if (ok) this.closeDocsPicker();
  }

  /** Fetches document bytes through the app's authenticated endpoint, uploads to
   *  MinIO via the mailbox attachment path, then persists the ref on the application. */
  private async attachBlobAsFile(fileName: string, fetchBlob: () => Promise<Blob>): Promise<boolean> {
    try {
      const blob = await fetchBlob();
      const file = new File([blob], fileName, { type: blob.type || 'application/pdf' });
      await this.uploadAttachments([file]);
      return true;
    } catch {
      this.attachmentError.set(`Could not load "${fileName}" from Documents`);
      return false;
    }
  }
}
