import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { ApplyService } from '@app/services/apply.service';
import { MailboxService } from '@app/services/mailbox.service';
import { ToastService } from '@app/services/toast.service';
import { DocumentsService } from '@app/services/documents.service';
import { AuthService } from '@app/services/auth.service';
import { ExtractorOutput, ExtractionHistoryItem } from '@app/models/extraction.types';
import { ScheduleTemplateDto, ApplyEmailResult } from '@app/models/apply.model';
import { CvDocumentDto } from '@app/models/document.model';
import { EmailAttachmentPayload } from '@app/models/apply.model';

interface CvOption {
  id: string;
  label: string;
}

const CRON_PRESETS: { label: string; cron: string }[] = [
  { label: 'Daily at 9:00', cron: '0 9 * * *' },
  { label: 'Weekly (Monday 9:00)', cron: '0 9 * * 1' },
  { label: 'Every 2 weeks (Monday 9:00)', cron: '0 9 * * 1/2' },
  { label: 'Monthly (1st 9:00)', cron: '0 9 1 * *' },
];

@Component({
  selector: 'app-apply-wizard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './apply-wizard.component.html',
  styleUrl: './apply-wizard.component.scss',
})
export class ApplyWizardComponent implements OnInit {
  private extractionSvc = inject(ExtractionService);
  private applySvc = inject(ApplyService);
  private mailboxSvc = inject(MailboxService);
  private docsSvc = inject(DocumentsService);
  private authSvc = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private toast = inject(ToastService);

  step = signal<1 | 2 | 3 | 4>(1);
  cronPresets = CRON_PRESETS;

  // Step 1
  inputText = '';
  inputUrl = '';
  language = 'en';
  history = signal<ExtractionHistoryItem[]>([]);
  extracting = signal(false);

  // Steps 2-3 shared
  extraction = signal<ExtractorOutput | null>(null);
  extractionId = signal<string | null>(null);
  savedToLibrary = signal(false);

  companyName = '';
  positionTitle = '';
  companyDescription = '';
  recipientEmail = '';
  recipientName = '';
  contactNotes = '';

  // Step 3 compose
  subject = '';
  body = '';
  templates = signal<ScheduleTemplateDto[]>([]);
  selectedTemplateId = signal<string>('');
  cvOptions = signal<CvOption[]>([]);
  selectedCvVersionId = signal<string>('');
  attachmentFiles = signal<File[]>([]);

  // Step 4 deliver
  deliverMode = signal<'now' | 'schedule'>('now');
  cronExpression = signal<string>(CRON_PRESETS[1].cron);

  submitting = signal(false);
  error = signal<string>('');
  result = signal<ApplyEmailResult | null>(null);
  gmailConnected = signal(false);
  gmailEmail = signal('');

  async ngOnInit() {
    this.history.set(await this.extractionSvc.getHistory('job-extractor').catch(() => []));
    this.templates.set((await this.mailboxSvc.getScheduleTemplates().catch(() => ({ success: true, data: [] }) as any)).data ?? []);
    const cvs = (await this.docsSvc.listCvs().catch(() => ({ success: true, data: [] }) as any)).data ?? [];
    const opts: CvOption[] = [];
    for (const cv of cvs as CvDocumentDto[]) {
      for (const v of cv.versions) opts.push({ id: v.id, label: `${cv.title} — ${v.label}` });
    }
    this.cvOptions.set(opts);
    try {
      const gmail = await this.mailboxSvc.getGmailStatus();
      this.gmailConnected.set(gmail.connected);
      if (gmail.email) this.gmailEmail.set(gmail.email);
    } catch { /* gmail status optional */ }
    await this.applyQueryParams();
  }

  private async applyQueryParams() {
    const q = this.route.snapshot.queryParamMap;
    const extractionId = q.get('extractionId');
    const companyName = q.get('companyName');
    const positionTitle = q.get('positionTitle');
    if (extractionId) {
      try {
        const output = await this.extractionSvc.getExtraction(extractionId);
        this.extractionId.set(extractionId);
        this.applyExtraction(output, extractionId);
        this.step.set(2);
        return;
      } catch { /* fall through to prefill */ }
    }
    if (companyName) {
      this.companyName = companyName;
      if (positionTitle) this.positionTitle = positionTitle;
      this.prefillCompose();
    }
  }

  async extract() {
    if (!this.inputText.trim() && !this.inputUrl.trim()) {
      this.error.set('Paste a job post or provide a URL first.');
      return;
    }
    this.error.set('');
    this.extracting.set(true);
    try {
      const res = await this.extractionSvc.extract({
        text: this.inputText.trim() || undefined,
        url: this.inputUrl.trim() || undefined,
        language: this.language,
      });
      this.extractionId.set(res.id);
      this.applyExtraction(res.output, res.id);
      this.step.set(2);
    } catch (e: any) {
      this.error.set(e?.message || 'Extraction failed');
    } finally {
      this.extracting.set(false);
    }
  }

  async selectHistory(id: string) {
    this.error.set('');
    try {
      const output = await this.extractionSvc.getExtraction(id);
      const item = this.history().find(h => h.id === id);
      this.extractionId.set(id);
      this.applyExtraction(output, id);
      this.step.set(2);
    } catch (e: any) {
      this.error.set(e?.message || 'Failed to load extraction');
    }
  }

  private applyExtraction(output: ExtractorOutput, id: string) {
    this.extraction.set(output);
    this.companyName = output.enterpriseName || this.companyName;
    this.positionTitle = output.jobRole || this.positionTitle;
    this.companyDescription = output.enterpriseDescription || '';
    this.recipientEmail = output.contactEmail || this.recipientEmail;
    this.prefillCompose();
  }

  private prefillCompose() {
    const company = this.companyName || '{{company_name}}';
    const role = this.positionTitle || '{{position}}';
    this.subject = `Application for the ${role} position at ${company}`;
    this.body =
      `Dear ${company} hiring team,\n\n` +
      `I am writing to express my interest in the ${role} role at ${company}. ` +
      `Please find my CV attached. I would be glad to discuss how my background fits your needs.\n\n` +
      `Best regards,\n{{my_name}}`;
  }

  useTemplate() {
    const tpl = this.templates().find(t => t.id === this.selectedTemplateId());
    if (!tpl) return;
    const rendered = this.renderTemplate(tpl.subjectTemplate, tpl.bodyTemplate);
    this.subject = rendered.subject;
    this.body = rendered.body;
    if (tpl.cvVersionId) this.selectedCvVersionId.set(tpl.cvVersionId);
  }

  /** Client-side best-effort token resolution so the wizard shows concrete text. */
  private renderTemplate(subjectTpl: string, bodyTpl: string) {
    const user = this.authSvc.currentUser();
    const myName = user ? `${user.firstName} ${user.lastName}`.trim() : '';
    const map: Record<string, string> = {
      '{{company_name}}': this.companyName,
      '{{company_description}}': this.companyDescription,
      '{{my_name}}': myName,
      '{{my_email}}': user?.email ?? '',
      '{{my_phone}}': '',
    };
    const replace = (s: string) => s.replace(/\{\{\s*(\w+)\s*\}\}/g, (m, k) => map[m.toLowerCase()] ?? map[m] ?? (map[k] ?? m));
    return { subject: replace(subjectTpl), body: replace(bodyTpl) };
  }

  async saveToLibrary() {
    const id = this.extractionId();
    if (!id) return;
    try {
      const res = await this.extractionSvc.saveToLibrary(id);
      this.savedToLibrary.set(true);
      this.error.set('');
      alert(`Saved to library: ${res.companyName}`);
    } catch (e: any) {
      this.error.set(e?.message || 'Failed to save to library');
    }
  }

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files) this.attachmentFiles.set([...this.attachmentFiles(), ...Array.from(input.files)]);
  }

  removeAttachment(i: number) {
    this.attachmentFiles.update(list => list.filter((_, idx) => idx !== i));
  }

  async submit() {
    if (!this.companyName.trim()) { this.error.set('Company name is required'); return; }
    if (!this.recipientEmail.trim()) { this.error.set('Recipient email is required'); return; }
    if (!this.subject.trim() || !this.body.trim()) { this.error.set('Subject and body are required'); return; }

    if (this.deliverMode() === 'now' && !this.gmailConnected()) {
      this.error.set('Gmail is not connected. Connect Gmail in Mailbox → Settings, or choose Schedule instead.');
      this.toast.error('Gmail not connected');
      return;
    }

    this.error.set('');
    this.submitting.set(true);
    try {
      const attachments: EmailAttachmentPayload[] = [];
      for (const f of this.attachmentFiles()) {
        const base64 = await this.fileToBase64(f);
        attachments.push({ fileName: f.name, contentType: f.type || 'application/octet-stream', contentBase64: base64 });
      }

      const res = await this.applySvc.apply({
        companyName: this.companyName.trim(),
        positionTitle: this.positionTitle.trim(),
        companyDescription: this.companyDescription || undefined,
        recipientEmail: this.recipientEmail.trim(),
        recipientName: this.recipientName.trim() || undefined,
        contactNotes: this.contactNotes.trim() || undefined,
        subject: this.subject.trim(),
        body: this.body,
        cvVersionId: this.selectedCvVersionId() || undefined,
        attachments: attachments.length ? attachments : undefined,
        scheduleCron: this.deliverMode() === 'schedule' ? this.cronExpression() : undefined,
        scheduleName: this.deliverMode() === 'schedule' ? `Apply → ${this.companyName.trim()}` : undefined,
        allowDuplicate: false,
      });

      if (!res.success || !res.data) {
        // Surface duplicate matches if present
        const payload = (res as any).errors;
        if (payload && payload.matches) {
          this.error.set(`This company already has ${payload.matches.length} application(s). Open the existing one or allow duplicate.`);
        } else {
          this.error.set(res.message || 'Apply failed');
        }
        return;
      }
      this.result.set(res.data);
      this.step.set(4);
    } catch (e: any) {
      this.error.set(e?.message || 'Apply failed');
    } finally {
      this.submitting.set(false);
    }
  }

  private fileToBase64(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => {
        const result = reader.result as string;
        resolve(result.split(',')[1]);
      };
      reader.onerror = reject;
      reader.readAsDataURL(file);
    });
  }

  goApplications() { this.router.navigate(['/applications/kanban']); }
  goDetail(id: string) { this.router.navigate(['/applications', id]); }
}
