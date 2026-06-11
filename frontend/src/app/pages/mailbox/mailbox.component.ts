import { Component, signal, inject, OnInit, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MailboxService } from '@app/services/mailbox.service';
import {
  ContactDto, EmailMessageDto, EmailScheduleDto,
  MailboxStatsDto, SendEmailDto, ContactHistoryResponse,
  GenerateEmailRequestDto,
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
  imports: [CommonModule, FormsModule],
  templateUrl: './mailbox.component.html',
  styleUrl: './mailbox.component.scss',
})
export class MailboxComponent implements OnInit {
  private service = inject(MailboxService);

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
  contactFormName = signal('');
  contactFormEmail = signal('');
  contactFormPhone = signal('');
  contactFormCompany = signal('');
  contactFormPosition = signal('');
  editingContactId = signal<string | null>(null);

  gmailConnected = signal(false);
  gmailEmail = signal('');

  // Compose aside
  asideTab = signal<'templates' | 'attachments' | 'generate'>('templates');
  templates = EMAIL_TEMPLATES;
  activeTemplate = signal<string | null>(null);
  attachments = signal<File[]>([]);

  // AI email generation
  aiJobTitle = signal('');
  aiCompanyName = signal('');
  aiJobDesc = signal('');
  aiHint = signal('');
  aiCandidateContext = signal('');
  aiGenerating = signal(false);
  aiError = signal<string | null>(null);
  aiGenerated = signal(false);

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
      const res = await this.service.getContacts({
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
      const res = await this.service.getContactHistory(contact.id);
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
    const res = await this.service.toggleFavorite(contact.id);
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
    const res = await this.service.updateContact(contact.id, dto);
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
      const res = await this.service.updateContact(contact.id, { avatarBase64: base64 });
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
    try {
      const dto: SendEmailDto = {
        recipientIds: this.composeRecipients().map(c => c.id),
        subject: this.composeSubject(),
        body: this.composeBody(),
      };
      const files = this.attachments();
      if (files.length > 0) {
        dto.attachmentBase64 = await this.readFileAsBase64(files[0]);
        dto.attachmentFileName = files[0].name;
      }
      const res = await this.service.send(dto);
      if (res.success) {
        this.composeRecipients.set([]);
        this.composeSubject.set('');
        this.composeBody.set('');
        this.attachments.set([]);
      }
    } catch {} finally { this.composeSending.set(false); }
  }

  private readFileAsBase64(file: File): Promise<string> {
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

  async generateEmail() {
    if (!this.aiJobTitle() || !this.aiCompanyName() || !this.aiJobDesc()) return;
    this.aiGenerating.set(true);
    this.aiError.set(null);
    this.aiGenerated.set(false);
    try {
      const dto: GenerateEmailRequestDto = {
        jobTitle: this.aiJobTitle(),
        companyName: this.aiCompanyName(),
        jobDescription: this.aiJobDesc(),
        coverLetterHint: this.aiHint() || undefined,
        candidateContext: this.aiCandidateContext() || undefined,
      };
      const res = await this.service.generateEmail(dto);
      if (res.success && res.data) {
        this.composeSubject.set(res.data.subject);
        this.composeBody.set(res.data.body);
        this.aiGenerated.set(true);
      } else {
        this.aiError.set('Generation failed. Please try again.');
      }
    } catch {
      this.aiError.set('Could not reach the generation service.');
    } finally {
      this.aiGenerating.set(false);
    }
  }

  async deleteContact(id: string) {
    await this.service.deleteContact(id);
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
      await this.service.updateContact(this.editingContactId()!, dto);
    } else {
      await this.service.createContact(dto);
    }
    this.showContactForm.set(false);
    this.loadContacts();
  }

  async importCsv(fileEvent: Event) {
    const file = (fileEvent.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const text = await file.text();
    await this.service.importCsv(text);
    this.loadContacts();
  }

  async importFromOffers() {
    await this.service.importFromOffers();
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
