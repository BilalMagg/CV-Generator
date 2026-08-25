import { Component, signal, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ApplicationService } from '@app/services/application.service';
import { ContactService } from '@app/services/contact.service';
import { AuthService } from '@app/services/auth.service';
import {
  DuplicateMatchDto,
  ApiResponse,
  ApplicationResponseDto,
  ApplicationStatus,
  ApplicationPriority,
  AttemptChannel,
  AttemptInitiatedBy,
  ContactSummaryDto,
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
export class ApplicationCreateComponent {
  private appService = inject(ApplicationService);
  private contactApi = inject(ContactService);
  private authService = inject(AuthService);
  protected router = inject(Router);

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
  stage = signal<'SAVED' | 'APPLIED'>('SAVED');

  // ── First attempt (only used when stage === 'APPLIED') ──────────────────────
  channel = signal<AttemptChannel>('EMAIL_GMAIL');
  initiatedBy = signal<AttemptInitiatedBy>('USER');
  markSent = signal(true);
  subject = signal('');
  body = signal('');
  recipientName = signal('');
  recipientContact = signal('');

  // Contact linking for the first attempt (search-all — no app id exists yet)
  pickedContact = signal<ContactSummaryDto | null>(null);
  contactSearch = signal('');
  contactResults = signal<ContactSummaryDto[]>([]);
  searchingContacts = signal(false);

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
    if (!this.recipientName().trim()) this.recipientName.set(c.name);
    if (!this.recipientContact().trim()) this.recipientContact.set(c.email);
    this.contactSearch.set('');
    this.contactResults.set([]);
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
    { value: 'WEB_FORM',             label: 'Web form',           icon: 'ti-world' },
    { value: 'IN_PERSON',            label: 'In person',          icon: 'ti-users-group' },
    { value: 'OTHER',                label: 'Other',              icon: 'ti-dots' },
  ];

  protected readonly INITIATORS: { value: AttemptInitiatedBy; label: string }[] = [
    { value: 'USER', label: 'Me' },
    { value: 'AI_AGENT', label: 'AI agent' },
    { value: 'SCHEDULE', label: 'Scheduled' },
  ];

  get isFormValid(): boolean {
    return this.companyName().trim().length > 0 && this.positionTitle().trim().length > 0;
  }

  setStage(stage: 'SAVED' | 'APPLIED') { this.stage.set(stage); }
  dismissDuplicates() { this.duplicateWarning.set(null); }

  // ── Live preview card ───────────────────────────────────────────────────────
  previewStatus = computed<ApplicationStatus>(() => (this.stage() === 'APPLIED' ? 'APPLIED' : 'SAVED'));
  previewDate = computed(() => this.stage() === 'APPLIED' ? new Date() : null);

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
      });

      if (!res.success || !res.data) {
        this.error.set(res.message || 'Failed to create');
        return;
      }

      const appId = res.data.id;

      // Log the first apply attempt — a SENT attempt flips the app to APPLIED server-side.
      if (this.stage() === 'APPLIED') {
        try {
          await this.appService.createAttempt(appId, {
            channel: this.channel(),
            initiatedBy: this.initiatedBy(),
            status: this.markSent() ? 'SENT' : 'DRAFT',
            subject: this.subject().trim() || undefined,
            body: this.body().trim() || undefined,
            recipientName: this.recipientName().trim() || undefined,
            recipientContact: this.recipientContact().trim() || undefined,
            contactId: this.pickedContact()?.id,
            sentAt: this.markSent() ? new Date().toISOString() : undefined,
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
