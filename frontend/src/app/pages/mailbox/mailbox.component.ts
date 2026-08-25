import { Component, signal, inject, OnInit, computed, effect } from '@angular/core';
import { SheetImportDialogComponent } from '@app/shared/components/sheet-import-dialog/sheet-import-dialog.component';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MailboxService } from '@app/services/mailbox.service';
import { ContactService } from '@app/services/contact.service';
import { ToastService } from '@app/services/toast.service';
import {
  ContactDto, EmailMessageDto, EmailScheduleDto,
  MailboxStatsDto, SendEmailDto, ContactHistoryResponse, EmailAttachmentPayload,
} from '@app/models/mailbox.model';

type MailboxView = 'compose' | 'history' | 'contacts' | 'schedules' | 'settings';

interface EmailTemplate {
  name: string;
  subject: string;
  body: string;
}

const EMAIL_TEMPLATES: EmailTemplate[] = [
  {
    name: 'Initial Outreach',
    subject: 'Application for Software Engineering Position',
    body: `Dear Hiring Manager,\n\nI am writing to express my strong interest in the Software Engineering position. With my background in full-stack development and passion for building scalable systems, I believe I would be a great addition to your team.\n\nI have attached my resume for your review and would welcome the opportunity to discuss how my skills align with your needs.\n\nBest regards,\n[Your Name]`,
  },
  {
    name: 'Follow-up',
    subject: 'Follow-up on Application',
    body: `Dear Hiring Manager,\n\nI hope this message finds you well. I wanted to follow up on my application submitted recently. I remain very interested in the position and would love to hear about any updates regarding the hiring process.\n\nPlease let me know if you need any additional information from me.\n\nBest regards,\n[Your Name]`,
  },
  {
    name: 'Interview Thank You',
    subject: 'Thank You for the Interview',
    body: `Dear Interviewer,\n\nThank you so much for taking the time to speak with me today. I truly enjoyed learning more about the team and the exciting work you are doing.\n\nOur conversation reinforced my enthusiasm for the role and I am confident that my skills and experience would be a great fit.\n\nI look forward to hearing about the next steps.\n\nBest regards,\n[Your Name]`,
  },
  {
    name: 'Status Update Request',
    subject: 'Application Status Inquiry',
    body: `Dear Hiring Manager,\n\nI hope you are doing well. I wanted to kindly check in on the status of my application. I remain very interested in the position and am eager to hear any updates.\n\nThank you for your time and consideration.\n\nBest regards,\n[Your Name]`,
  },
];

@Component({
  selector: 'app-mailbox',
  standalone: true,
  imports: [CommonModule, FormsModule, SheetImportDialogComponent],
  templateUrl: './mailbox.component.html',
  styleUrl: './mailbox.component.scss',
})
export class MailboxComponent implements OnInit {
  private service = inject(MailboxService);
  private contactApi = inject(ContactService);
  readonly toast = inject(ToastService);

  view = signal<MailboxView>((localStorage.getItem('mailbox-view') as MailboxView) || 'compose');

  constructor() {
    effect(() => localStorage.setItem('mailbox-view', this.view()));
  }
  loading = signal(true);
  stats = signal<MailboxStatsDto | null>(null);

  contacts = signal<ContactDto[]>([]);
  contactsLoading = signal(false);
  contactsTotal = signal(0);
  contactSearch = signal('');
  showFavoritesOnly = signal(false);
  contactDetailView = signal(false);
  selectedContactDetail = signal<ContactDto | null>(null);
  contactHistory = signal<EmailMessageDto[]>([]);
  contactHistoryLoading = signal(false);
  cdEditing = signal(false);
  cdEditName = signal('');
  cdEditEmail = signal('');
  cdEditCompany = signal('');
  cdEditPosition = signal('');
  cdEditPhone = signal('');

  history = signal<EmailMessageDto[]>([]);
  historyLoading = signal(false);
  historyTotal = signal(0);
  historyPage = signal(1);
  historySearch = signal('');
  selectedEmail = signal<EmailMessageDto | null>(null);
  selectedEmailLoading = signal(false);

  schedules = signal<EmailScheduleDto[]>([]);
  schedulesLoading = signal(false);

  composeRecipientSearch = signal('');
  composeRecipients = signal<ContactDto[]>([]);
  composeSubject = signal('');
  composeBody = signal('');
  composeSending = signal(false);

  selectedContact = signal<ContactDto | null>(null);
  showContactForm = signal(false);
  sheetImportOpen = signal(false);
  contactFormName = signal('');
  contactFormEmail = signal('');
  contactFormPhone = signal('');
  contactFormCompany = signal('');
  contactFormPosition = signal('');
  editingContactId = signal<string | null>(null);

  gmailConnected = signal(false);
  gmailEmail = signal('');

  // Compose aside
  asideTab = signal<'templates' | 'attachments'>('templates');
  templates = EMAIL_TEMPLATES;
  activeTemplate = signal<string | null>(null);
  attachments = signal<File[]>([]);

  // Contact modal
  showContactModal = signal(false);
  modalContactSearch = signal('');
  modalSelectedIds = signal<Set<string>>(new Set());

  sortedContacts = computed(() =>
    [...this.contacts()].sort((a, b) => a.name.localeCompare(b.name))
  );

  filteredContacts = computed(() => {
    const q = this.composeRecipientSearch().toLowerCase();
    let list = this.contacts();
    if (this.showFavoritesOnly()) list = list.filter(c => c.isFavorite);
    if (!q) return list;
    return list.filter(c =>
      c.name.toLowerCase().includes(q) || c.email.toLowerCase().includes(q)
    );
  });

  modalFilteredContacts = computed(() => {
    const q = this.modalContactSearch().toLowerCase();
    let list = this.sortedContacts();
    if (this.showFavoritesOnly()) list = list.filter(c => c.isFavorite);
    if (q) {
      list = list.filter(c =>
        c.name.toLowerCase().includes(q) || c.email.toLowerCase().includes(q)
      );
    }
    return list;
  });

  async ngOnInit() {
    this.loading.set(true);
    try {
      const [statsRes] = await Promise.all([
        this.service.getStats(),
      ]);
      if (statsRes.success && statsRes.data) this.stats.set(statsRes.data);
    } catch { } finally { this.loading.set(false); }
    this.loadContacts();
    this.loadHistory();
    this.loadSchedules();
    this.loadGmailStatus();
  }

  async loadGmailStatus() {
    try {
      const res = await this.service.getGmailStatus();
      this.gmailConnected.set(res.connected);
      if (res.email) this.gmailEmail.set(res.email);
    } catch {}
  }

  async disconnectGmail() {
    try {
      await this.service.disconnectGmail();
      this.gmailConnected.set(false);
      this.gmailEmail.set('');
    } catch {}
  }

  async loadContacts() {
    this.contactsLoading.set(true);
    try {
      const res = await this.contactApi.getContacts({
        search: this.contactSearch(),
        favorite: this.showFavoritesOnly() || undefined,
      });
      if (res.success && res.data) {
        this.contacts.set(res.data.items);
        this.contactsTotal.set(res.data.total);
      }
    } catch {} finally { this.contactsLoading.set(false); }
  }

  async loadHistory() {
    this.historyLoading.set(true);
    try {
      const res = await this.service.getHistory({
        page: this.historyPage(),
        pageSize: 20,
        search: this.historySearch() || undefined,
      });
      if (res.success && res.data) {
        this.history.set(res.data.items);
        this.historyTotal.set(res.data.total);
      }
    } catch {} finally { this.historyLoading.set(false); }
  }

  async loadHistoryDetail(id: string) {
    this.selectedEmailLoading.set(true);
    try {
      const res = await this.service.getHistoryDetail(id);
      if (res.success && res.data) this.selectedEmail.set(res.data);
    } catch {} finally { this.selectedEmailLoading.set(false); }
  }

  selectEmail(email: EmailMessageDto) {
    this.selectedEmail.set(email);
  }

  backToHistoryList() {
    this.selectedEmail.set(null);
  }

  async loadContactHistory(contact: ContactDto) {
    this.contactDetailView.set(true);
    this.selectedContactDetail.set(contact);
    this.contactHistoryLoading.set(true);
    try {
      const res = await this.contactApi.getContactHistory(contact.id);
      if (res.success && res.data) {
        this.contactHistory.set(res.data.emails);
      }
    } catch {} finally { this.contactHistoryLoading.set(false); }
  }

  backToContactList() {
    this.contactDetailView.set(false);
    this.selectedContactDetail.set(null);
    this.contactHistory.set([]);
  }

  async toggleFavorite(contact: ContactDto) {
    const res = await this.contactApi.toggleFavorite(contact.id);
    if (res.success && res.data) {
      this.contacts.set(this.contacts().map(c => c.id === contact.id ? { ...c, isFavorite: res.data!.isFavorite } : c));
    }
  }

  startContactEdit(contact: ContactDto) {
    this.cdEditName.set(contact.name);
    this.cdEditEmail.set(contact.email);
    this.cdEditCompany.set(contact.company || '');
    this.cdEditPosition.set(contact.position || '');
    this.cdEditPhone.set(contact.phone || '');
    this.cdEditing.set(true);
  }

  cancelContactEdit() {
    this.cdEditing.set(false);
  }

  async saveContactDetail() {
    const contact = this.selectedContactDetail();
    if (!contact) return;
    const dto: any = {
      name: this.cdEditName(),
      email: this.cdEditEmail(),
      company: this.cdEditCompany() || undefined,
      position: this.cdEditPosition() || undefined,
      phone: this.cdEditPhone() || undefined,
    };
    const res = await this.contactApi.updateContact(contact.id, dto);
    if (res.success && res.data) {
      this.selectedContactDetail.set(res.data);
      this.cdEditing.set(false);
      this.contacts.set(this.contacts().map(c => c.id === contact.id ? res.data! : c));
    }
  }

  onContactAvatarChange(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input?.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = async () => {
      const base64 = reader.result as string;
      const contact = this.selectedContactDetail();
      if (!contact) return;
      const res = await this.contactApi.updateContact(contact.id, { avatarBase64: base64 });
      if (res.success && res.data) {
        this.selectedContactDetail.set(res.data);
        this.contacts.set(this.contacts().map(c => c.id === contact.id ? res.data! : c));
      }
    };
    reader.readAsDataURL(file);
    input.value = '';
  }

  async loadSchedules() {
    this.schedulesLoading.set(true);
    try {
      const res = await this.service.getSchedules();
      if (res.success && res.data) this.schedules.set(res.data);
    } catch {} finally { this.schedulesLoading.set(false); }
  }

  async sendEmail() {
    if (!this.composeRecipients().length || !this.composeSubject() || !this.composeBody()) return;
    this.composeSending.set(true);
    const recipients = this.composeRecipients();
    try {
      const files = this.attachments();
      const totalBytes = files.reduce((sum, f) => sum + f.size, 0);
      if (totalBytes > 10 * 1024 * 1024) {
        this.toast.error('Attachments exceed the 10 MB limit');
        return;
      }
      const attachmentPayloads: EmailAttachmentPayload[] = [];
      for (const file of files) {
        const base64 = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = () => resolve((reader.result as string).split(',')[1] ?? '');
          reader.onerror = () => reject(reader.error);
          reader.readAsDataURL(file);
        });
        attachmentPayloads.push({ fileName: file.name, contentType: file.type || 'application/octet-stream', contentBase64: base64 });
      }

      const dto: SendEmailDto = {
        recipientIds: recipients.map(c => c.id),
        subject: this.composeSubject(),
        body: this.composeBody(),
        attachments: attachmentPayloads.length ? attachmentPayloads : undefined,
      };
      const res = await this.service.send(dto);
      if (res.success) {
        const to = recipients.length === 1
          ? (recipients[0].name || recipients[0].email)
          : `${recipients.length} recipients`;
        this.toast.success(`Email sent to ${to}`);
        this.composeRecipients.set([]);
        this.composeSubject.set('');
        this.composeBody.set('');
        this.loadHistory();
        this.loadStats();
      } else {
        this.toast.error(res.message || 'Failed to send email');
      }
    } catch {
      this.toast.error('Failed to send email — check your connection or Gmail status');
    } finally { this.composeSending.set(false); }
  }

  async loadStats() {
    try {
      const res = await this.service.getStats();
      if (res.success && res.data) this.stats.set(res.data);
    } catch {}
  }

  /** True for emails sent in the last few seconds — drives the green flash on the history row. */
  isJustSent(m: EmailMessageDto): boolean {
    if (m.status !== 'sent' || !m.sentAt) return false;
    return Date.now() - new Date(m.sentAt).getTime() < 10_000;
  }

  formatBytes(bytes: number): string {
    if (!bytes || bytes < 0) return '0 KB';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  async deleteContact(id: string) {
    await this.contactApi.deleteContact(id);
    this.loadContacts();
  }

  async toggleSchedule(id: string) {
    await this.service.toggleSchedule(id);
    this.loadSchedules();
  }

  async deleteSchedule(id: string) {
    await this.service.deleteSchedule(id);
    this.loadSchedules();
  }

  startNewContact() {
    this.showContactForm.set(true);
    this.editingContactId.set(null);
    this.contactFormName.set('');
    this.contactFormEmail.set('');
    this.contactFormPhone.set('');
    this.contactFormCompany.set('');
    this.contactFormPosition.set('');
  }

  editContact(c: ContactDto) {
    this.showContactForm.set(true);
    this.editingContactId.set(c.id);
    this.contactFormName.set(c.name);
    this.contactFormEmail.set(c.email);
    this.contactFormPhone.set(c.phone || '');
    this.contactFormCompany.set(c.company || '');
    this.contactFormPosition.set(c.position || '');
  }

  async saveContact() {
    const dto = {
      name: this.contactFormName(),
      email: this.contactFormEmail(),
      phone: this.contactFormPhone() || undefined,
      company: this.contactFormCompany() || undefined,
      position: this.contactFormPosition() || undefined,
    };
    if (this.editingContactId()) {
      await this.contactApi.updateContact(this.editingContactId()!, dto);
    } else {
      await this.contactApi.createContact(dto);
    }
    this.showContactForm.set(false);
    this.loadContacts();
  }

  async importCsv(fileEvent: Event) {
    const file = (fileEvent.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const text = await file.text();
    await this.contactApi.importCsv(text);
    this.loadContacts();
  }

  async importFromOffers() {
    await this.contactApi.importFromOffers();
    this.loadContacts();
  }

  toggleRecipient(c: ContactDto) {
    const curr = this.composeRecipients();
    const exists = curr.find(r => r.id === c.id);
    if (exists) {
      this.composeRecipients.set(curr.filter(r => r.id !== c.id));
    } else {
      this.composeRecipients.set([...curr, c]);
    }
  }

  isRecipientSelected(c: ContactDto): boolean {
    return this.composeRecipients().some(r => r.id === c.id);
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  formatDateTime(d: string): string {
    return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
  }

  relativeTime(d: string): string {
    const diff = Date.now() - new Date(d).getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 60) return mins + 'm ago';
    const hours = Math.floor(mins / 60);
    if (hours < 24) return hours + 'h ago';
    const days = Math.floor(hours / 24);
    if (days < 30) return days + 'd ago';
    return this.formatDate(d);
  }

  applyTemplate(t: EmailTemplate) {
    this.composeSubject.set(t.subject);
    this.composeBody.set(t.body);
    this.activeTemplate.set(t.name);
  }

  onFilesSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (!input.files?.length) return;
    const files = Array.from(input.files);
    this.attachments.set([...this.attachments(), ...files]);
    input.value = '';
  }

  removeAttachment(file: File) {
    this.attachments.set(this.attachments().filter(f => f !== file));
  }

  openContactModal() {
    this.modalSelectedIds.set(new Set(this.composeRecipients().map(c => c.id)));
    this.modalContactSearch.set('');
    this.showContactModal.set(true);
  }

  closeContactModal() {
    this.showContactModal.set(false);
  }

  toggleModalContact(c: ContactDto) {
    const set = new Set(this.modalSelectedIds());
    if (set.has(c.id)) set.delete(c.id); else set.add(c.id);
    this.modalSelectedIds.set(set);
  }

  onAddSelected() {
    const ids = this.modalSelectedIds();
    const selected = this.contacts().filter(c => ids.has(c.id));
    this.composeRecipients.set(selected);
    this.showContactModal.set(false);
  }
}
