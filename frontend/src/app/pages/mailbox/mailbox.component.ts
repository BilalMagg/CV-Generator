import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@app/services/auth.service';
import { MailboxService } from '@app/services/mailbox.service';
import {
  ContactDto, EmailMessageDto, EmailScheduleDto,
  MailboxStatsDto, SendEmailDto,
} from '@app/models/mailbox.model';

type MailboxView = 'compose' | 'history' | 'contacts' | 'schedules' | 'settings';

@Component({
  selector: 'app-mailbox',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './mailbox.component.html',
  styleUrl: './mailbox.component.scss',
})
export class MailboxComponent implements OnInit {
  private auth = inject(AuthService);
  private service = inject(MailboxService);

  view = signal<MailboxView>('compose');
  loading = signal(true);
  stats = signal<MailboxStatsDto | null>(null);

  userId = computed(() => this.auth.currentUser()?.userId ?? '');

  contacts = signal<ContactDto[]>([]);
  contactsLoading = signal(false);
  contactsTotal = signal(0);
  contactSearch = signal('');

  history = signal<EmailMessageDto[]>([]);
  historyLoading = signal(false);
  historyTotal = signal(0);
  historyPage = signal(1);

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

  async ngOnInit() {
    if (!this.userId()) return;
    this.loading.set(true);
    try {
      const [statsRes] = await Promise.all([
        this.service.getStats(this.userId()),
      ]);
      if (statsRes.success && statsRes.data) this.stats.set(statsRes.data);
    } catch { } finally { this.loading.set(false); }
    this.loadContacts();
    this.loadHistory();
    this.loadSchedules();
  }

  async loadContacts() {
    this.contactsLoading.set(true);
    try {
      const res = await this.service.getContacts(this.userId(), { search: this.contactSearch() });
      if (res.success && res.data) {
        this.contacts.set(res.data.items);
        this.contactsTotal.set(res.data.total);
      }
    } catch {} finally { this.contactsLoading.set(false); }
  }

  async loadHistory() {
    this.historyLoading.set(true);
    try {
      const res = await this.service.getHistory(this.userId(), { page: this.historyPage(), pageSize: 20 });
      if (res.success && res.data) {
        this.history.set(res.data.items);
        this.historyTotal.set(res.data.total);
      }
    } catch {} finally { this.historyLoading.set(false); }
  }

  async loadSchedules() {
    this.schedulesLoading.set(true);
    try {
      const res = await this.service.getSchedules(this.userId());
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
      const res = await this.service.send(this.userId(), dto);
      if (res.success) {
        this.composeRecipients.set([]);
        this.composeSubject.set('');
        this.composeBody.set('');
      }
    } catch {} finally { this.composeSending.set(false); }
  }

  async deleteContact(id: string) {
    await this.service.deleteContact(this.userId(), id);
    this.loadContacts();
  }

  async toggleSchedule(id: string) {
    await this.service.toggleSchedule(this.userId(), id);
    this.loadSchedules();
  }

  async deleteSchedule(id: string) {
    await this.service.deleteSchedule(this.userId(), id);
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
      await this.service.updateContact(this.userId(), this.editingContactId()!, dto);
    } else {
      await this.service.createContact(this.userId(), dto);
    }
    this.showContactForm.set(false);
    this.loadContacts();
  }

  async importCsv(fileEvent: Event) {
    const file = (fileEvent.target as HTMLInputElement).files?.[0];
    if (!file) return;
    const text = await file.text();
    await this.service.importCsv(this.userId(), text);
    this.loadContacts();
  }

  async importFromOffers() {
    await this.service.importFromOffers(this.userId());
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

  filteredContacts = computed(() => {
    const q = this.composeRecipientSearch().toLowerCase();
    if (!q) return this.contacts();
    return this.contacts().filter(c =>
      c.name.toLowerCase().includes(q) || c.email.toLowerCase().includes(q)
    );
  });

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
}
