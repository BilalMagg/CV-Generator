import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MailboxService } from '../../../services/mailbox.service';
import {
  ContactDto,
  BulkGenerateContactRequestDto,
  BulkGenerateContactResultDto,
  SendEmailDto,
  CreateScheduleDto,
  CsvImportResultDto,
} from '../../../models/mailbox.model';

// ─── Local interfaces ──────────────────────────────────────────────────────

interface DraftState {
  contactId: string;
  contactName: string;
  contactEmail: string;
  subject: string;
  body: string;
  error?: string;
  selected: boolean;
  sent: boolean;
  sendError?: string;
}

interface CsvPreviewRow {
  name: string;
  email: string;
  phone: string;
  company: string;
  position: string;
  notes: string;
  valid: boolean;
}

type MainTab = 'campaign' | 'import';
type CampaignStep = 'select' | 'context' | 'review';
type FrequencyPreset = 'daily' | 'weekly' | 'monthly' | 'custom';

const CSV_TEMPLATE = [
  'name,email,phone,company,position,notes',
  'John Doe,john.doe@example.com,+1234567890,Acme Corp,Software Engineer,Met at tech conference',
  'Jane Smith,jane.smith@example.com,,TechCo,Product Manager,',
  'Alex Johnson,alex.j@example.com,+9876543210,StartupXYZ,CTO,',
].join('\n');

@Component({
  selector: 'app-contact-agent-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './contact-agent-workspace.component.html',
  styleUrl: './contact-agent-workspace.component.scss',
})
export class ContactAgentWorkspaceComponent implements OnInit {
  private mailbox = inject(MailboxService);

  // ─── Tabs ────────────────────────────────────────────────────────────────
  mainTab = signal<MainTab>('campaign');

  // ─── Campaign — Step 1: Select Contacts ──────────────────────────────────
  contacts = signal<ContactDto[]>([]);
  selectedIds = signal<Set<string>>(new Set());
  searchQuery = signal('');
  loadingContacts = signal(false);
  contactsError = signal<string | null>(null);

  // ─── Campaign — Step 2: Job Context ──────────────────────────────────────
  jobTitle = signal('');
  companyName = signal('');
  jobDescription = signal('');
  coverLetterHint = signal('');
  candidateContext = signal('');
  generating = signal(false);
  generateError = signal<string | null>(null);

  // ─── Campaign — Step 3: Review & Send ────────────────────────────────────
  step = signal<CampaignStep>('select');
  drafts = signal<DraftState[]>([]);
  sending = signal(false);
  sendDone = signal(false);

  // ─── Scheduling ───────────────────────────────────────────────────────────
  scheduleOpen = signal(false);
  scheduleName = signal('');
  scheduleFrequency = signal<FrequencyPreset>('weekly');
  scheduleTime = signal('09:00');
  scheduleDay = signal('1');
  scheduleCron = signal('');
  scheduling = signal(false);
  scheduleError = signal<string | null>(null);
  scheduleSuccess = signal(false);

  // ─── CSV Import ───────────────────────────────────────────────────────────
  csvFile = signal<File | null>(null);
  csvRaw = signal('');
  csvPreviewRows = signal<CsvPreviewRow[]>([]);
  csvParseError = signal<string | null>(null);
  importing = signal(false);
  importResult = signal<CsvImportResultDto | null>(null);
  csvDragOver = signal(false);

  // ─── Computed ─────────────────────────────────────────────────────────────

  filteredContacts = computed(() => {
    const q = this.searchQuery().toLowerCase().trim();
    if (!q) return this.contacts();
    return this.contacts().filter(
      (c) =>
        c.name.toLowerCase().includes(q) ||
        c.email.toLowerCase().includes(q) ||
        (c.company?.toLowerCase().includes(q) ?? false)
    );
  });

  selectedContacts = computed(() =>
    this.contacts().filter((c) => this.selectedIds().has(c.id))
  );

  selectedDrafts = computed(() => this.drafts().filter((d) => d.selected && !d.error));

  canGenerate = computed(
    () =>
      this.selectedIds().size > 0 &&
      this.jobTitle().trim() !== '' &&
      this.companyName().trim() !== '' &&
      this.jobDescription().trim() !== ''
  );

  allSent = computed(() => {
    const sel = this.drafts().filter((d) => d.selected && !d.error);
    return sel.length > 0 && sel.every((d) => d.sent);
  });

  failedDraftsCount = computed(() => this.drafts().filter((d) => !!d.error).length);
  sentCount = computed(() => this.drafts().filter((d) => d.sent).length);
  readyCount = computed(() => this.drafts().length - this.failedDraftsCount());

  allFilteredSelected = computed(() => {
    const filtered = this.filteredContacts();
    return filtered.length > 0 && filtered.every((c) => this.selectedIds().has(c.id));
  });

  resolvedCron = computed(() => {
    const [h, m] = this.scheduleTime().split(':').map(Number);
    switch (this.scheduleFrequency()) {
      case 'daily':   return `${m} ${h} * * *`;
      case 'weekly':  return `${m} ${h} * * ${this.scheduleDay()}`;
      case 'monthly': return `${m} ${h} ${this.scheduleDay()} * *`;
      case 'custom':  return this.scheduleCron();
      default:        return '';
    }
  });

  // CSV computed
  csvPreviewDisplay = computed(() => this.csvPreviewRows().slice(0, 10));
  csvValidCount     = computed(() => this.csvPreviewRows().filter((r) => r.valid).length);
  csvInvalidCount   = computed(() => this.csvPreviewRows().filter((r) => !r.valid).length);
  csvHasData        = computed(() => this.csvPreviewRows().length > 0 && !this.csvParseError());

  // ─── Lifecycle ────────────────────────────────────────────────────────────

  ngOnInit(): void {
    this.loadContacts();
  }

  async loadContacts(): Promise<void> {
    this.loadingContacts.set(true);
    this.contactsError.set(null);
    try {
      const res = await this.mailbox.getContacts({ pageSize: 200 });
      if (res.success) {
        this.contacts.set(res.data?.items ?? []);
      } else {
        this.contactsError.set('Failed to load contacts');
      }
    } catch {
      this.contactsError.set('Failed to load contacts');
    } finally {
      this.loadingContacts.set(false);
    }
  }

  // ─── Tab switching ────────────────────────────────────────────────────────

  switchTab(tab: MainTab): void {
    this.mainTab.set(tab);
  }

  // ─── Campaign step navigation ─────────────────────────────────────────────

  toggleContact(id: string): void {
    const set = new Set(this.selectedIds());
    if (set.has(id)) set.delete(id);
    else set.add(id);
    this.selectedIds.set(set);
  }

  toggleAll(): void {
    const filtered = this.filteredContacts();
    const allSelected = filtered.every((c) => this.selectedIds().has(c.id));
    const set = new Set(this.selectedIds());
    if (allSelected) {
      filtered.forEach((c) => set.delete(c.id));
    } else {
      filtered.forEach((c) => set.add(c.id));
    }
    this.selectedIds.set(set);
  }

  goToContext(): void { this.step.set('context'); }
  backToSelect(): void { this.step.set('select'); }
  backToContext(): void {
    this.step.set('context');
    this.drafts.set([]);
    this.sendDone.set(false);
    this.scheduleOpen.set(false);
    this.scheduleSuccess.set(false);
  }

  // ─── Campaign — generate ──────────────────────────────────────────────────

  async generate(): Promise<void> {
    this.generating.set(true);
    this.generateError.set(null);

    const dto: BulkGenerateContactRequestDto = {
      contacts: this.selectedContacts().map((c) => ({
        id: c.id,
        name: c.name,
        email: c.email,
        company: c.company,
        position: c.position,
      })),
      jobTitle: this.jobTitle(),
      companyName: this.companyName(),
      jobDescription: this.jobDescription(),
      coverLetterHint: this.coverLetterHint() || undefined,
      candidateContext: this.candidateContext() || undefined,
    };

    try {
      const res = await this.mailbox.generateBulk(dto);
      if (res.success && res.data) {
        const drafts: DraftState[] = res.data.results.map(
          (r: BulkGenerateContactResultDto) => ({
            contactId: r.contactId,
            contactName: r.contactName,
            contactEmail: r.contactEmail,
            subject: r.subject ?? '',
            body: r.body ?? '',
            error: r.error,
            selected: !r.error,
            sent: false,
          })
        );
        this.drafts.set(drafts);
        this.scheduleName.set(`${this.jobTitle()} @ ${this.companyName()} — Campaign`);
        this.step.set('review');
      } else {
        this.generateError.set('Generation failed. Please try again.');
      }
    } catch (e: unknown) {
      this.generateError.set(
        (e as { message?: string })?.message ?? 'Generation failed. Please try again.'
      );
    } finally {
      this.generating.set(false);
    }
  }

  // ─── Campaign — draft editing ─────────────────────────────────────────────

  updateSubject(index: number, value: string): void {
    const drafts = [...this.drafts()];
    drafts[index] = { ...drafts[index], subject: value };
    this.drafts.set(drafts);
  }

  updateBody(index: number, value: string): void {
    const drafts = [...this.drafts()];
    drafts[index] = { ...drafts[index], body: value };
    this.drafts.set(drafts);
  }

  toggleDraft(index: number): void {
    const drafts = [...this.drafts()];
    drafts[index] = { ...drafts[index], selected: !drafts[index].selected };
    this.drafts.set(drafts);
  }

  // ─── Campaign — send ──────────────────────────────────────────────────────

  async sendCampaign(): Promise<void> {
    const toSend = this.drafts().filter((d) => d.selected && !d.sent && !d.error);
    if (toSend.length === 0) return;
    this.sending.set(true);

    for (const draft of toSend) {
      const dto: SendEmailDto = {
        recipientIds: [draft.contactId],
        subject: draft.subject,
        body: draft.body,
      };
      try {
        await this.mailbox.send(dto);
        this.markDraft(draft.contactId, { sent: true });
      } catch {
        this.markDraft(draft.contactId, { sendError: 'Send failed' });
      }
    }

    this.sending.set(false);
    this.sendDone.set(true);
  }

  // ─── Campaign — schedule ──────────────────────────────────────────────────

  toggleSchedule(): void {
    this.scheduleOpen.set(!this.scheduleOpen());
    this.scheduleError.set(null);
    this.scheduleSuccess.set(false);
  }

  async createSchedule(): Promise<void> {
    const cron = this.resolvedCron();
    if (!cron.trim()) {
      this.scheduleError.set('Please enter a valid cron expression');
      return;
    }
    const toSchedule = this.selectedDrafts();
    if (toSchedule.length === 0) {
      this.scheduleError.set('No drafts selected');
      return;
    }

    this.scheduling.set(true);
    this.scheduleError.set(null);

    let created = 0;
    for (const draft of toSchedule) {
      const dto: CreateScheduleDto = {
        name: `${this.scheduleName()} — ${draft.contactName}`,
        cronExpression: cron,
        subject: draft.subject,
        body: draft.body,
        recipientIds: [draft.contactId],
      };
      try {
        await this.mailbox.createSchedule(dto);
        created++;
      } catch {
        // continue on partial failure
      }
    }

    this.scheduling.set(false);
    if (created > 0) {
      this.scheduleSuccess.set(true);
      this.scheduleOpen.set(false);
    } else {
      this.scheduleError.set('Failed to create schedules. Please try again.');
    }
  }

  // ─── CSV import ───────────────────────────────────────────────────────────

  downloadTemplate(): void {
    const blob = new Blob([CSV_TEMPLATE], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'contacts_template.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  onCsvFileDrop(event: DragEvent): void {
    event.preventDefault();
    this.csvDragOver.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) this.loadCsvFile(file);
  }

  onCsvFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.loadCsvFile(file);
    input.value = '';
  }

  private loadCsvFile(file: File): void {
    if (!file.name.endsWith('.csv')) {
      this.csvParseError.set('Only .csv files are accepted');
      return;
    }
    this.csvFile.set(file);
    this.csvPreviewRows.set([]);
    this.csvParseError.set(null);
    this.importResult.set(null);

    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      this.csvRaw.set(content);
      this.parseCsvForPreview(content);
    };
    reader.readAsText(file, 'utf-8');
  }

  clearCsvFile(): void {
    this.csvFile.set(null);
    this.csvRaw.set('');
    this.csvPreviewRows.set([]);
    this.csvParseError.set(null);
    this.importResult.set(null);
  }

  private parseCsvForPreview(content: string): void {
    const normalized = content.replace(/^﻿/, '').replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    const lines = normalized.split('\n').filter((l) => l.trim() !== '');

    if (lines.length < 2) {
      this.csvParseError.set('The file must have a header row and at least one data row.');
      return;
    }

    const headers = this.splitCsvLine(lines[0]).map((h) => h.toLowerCase().trim());
    const nameIdx     = headers.indexOf('name');
    const emailIdx    = headers.indexOf('email');
    const phoneIdx    = headers.indexOf('phone');
    const companyIdx  = headers.indexOf('company');
    const positionIdx = headers.indexOf('position');
    const notesIdx    = headers.indexOf('notes');

    if (nameIdx < 0 || emailIdx < 0) {
      this.csvParseError.set(
        'Missing required columns. The CSV must have "name" and "email" headers.'
      );
      return;
    }

    const rows: CsvPreviewRow[] = lines.slice(1).map((line) => {
      const cols = this.splitCsvLine(line);
      const name  = cols[nameIdx]?.trim()  ?? '';
      const email = cols[emailIdx]?.trim() ?? '';
      return {
        name,
        email,
        phone:    phoneIdx    >= 0 ? (cols[phoneIdx]?.trim()    ?? '') : '',
        company:  companyIdx  >= 0 ? (cols[companyIdx]?.trim()  ?? '') : '',
        position: positionIdx >= 0 ? (cols[positionIdx]?.trim() ?? '') : '',
        notes:    notesIdx    >= 0 ? (cols[notesIdx]?.trim()    ?? '') : '',
        valid:    !!name && !!email,
      };
    });

    this.csvPreviewRows.set(rows);
    this.csvParseError.set(null);
  }

  private splitCsvLine(line: string): string[] {
    const fields: string[] = [];
    let current = '';
    let inQuotes = false;

    for (const char of line) {
      if (char === '"') {
        inQuotes = !inQuotes;
      } else if (char === ',' && !inQuotes) {
        fields.push(current.trim());
        current = '';
      } else {
        current += char;
      }
    }
    fields.push(current.trim());
    return fields;
  }

  async importContacts(): Promise<void> {
    const csvContent = this.csvRaw();
    if (!csvContent) return;

    this.importing.set(true);
    this.importResult.set(null);

    try {
      const res = await this.mailbox.importCsv(csvContent);
      if (res.success && res.data) {
        this.importResult.set(res.data);
        this.csvFile.set(null);
        this.csvRaw.set('');
        this.csvPreviewRows.set([]);
        await this.loadContacts();
      } else {
        this.csvParseError.set('Import failed. Please check your CSV and try again.');
      }
    } catch (e: unknown) {
      this.csvParseError.set(
        (e as { message?: string })?.message ?? 'Import failed. Please try again.'
      );
    } finally {
      this.importing.set(false);
    }
  }

  // ─── Helpers ──────────────────────────────────────────────────────────────

  getInitials(name: string): string {
    return name
      .split(' ')
      .map((w) => w[0] ?? '')
      .slice(0, 2)
      .join('')
      .toUpperCase();
  }

  private markDraft(contactId: string, patch: Partial<DraftState>): void {
    const drafts = [...this.drafts()];
    const idx = drafts.findIndex((d) => d.contactId === contactId);
    if (idx >= 0) drafts[idx] = { ...drafts[idx], ...patch };
    this.drafts.set(drafts);
  }
}
