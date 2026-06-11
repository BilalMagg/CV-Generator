export interface ContactDto {
  id: string;
  userId: string;
  name: string;
  email: string;
  phone?: string;
  company?: string;
  position?: string;
  source?: string;
  notes?: string;
  avatarBase64?: string;
  isFavorite: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateContactDto {
  name: string;
  email: string;
  phone?: string;
  company?: string;
  position?: string;
  notes?: string;
  isFavorite?: boolean;
  avatarBase64?: string;
}

export interface UpdateContactDto {
  name?: string;
  email?: string;
  phone?: string;
  company?: string;
  position?: string;
  notes?: string;
  isFavorite?: boolean;
  avatarBase64?: string;
}

export interface ImportCsvDto {
  csvContent: string;
}

export interface ContactListResponse {
  items: ContactDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface EmailMessageDto {
  id: string;
  userId: string;
  contactId?: string;
  recipientEmail: string;
  recipientName?: string;
  subject: string;
  body: string;
  status: string;
  provider: string;
  errorMessage?: string;
  sentAt?: string;
  createdAt: string;
}

export interface SendEmailDto {
  recipientIds: string[];
  subject: string;
  body: string;
  attachmentBase64?: string;
  attachmentFileName?: string;
}

export interface EmailHistoryResponse {
  items: EmailMessageDto[];
  total: number;
  sentCount: number;
  failedCount: number;
  draftCount: number;
}

export interface ContactHistoryResponse {
  contact: ContactDto;
  emails: EmailMessageDto[];
  totalEmails: number;
}

export interface EmailScheduleDto {
  id: string;
  userId: string;
  name: string;
  cronExpression: string;
  subject: string;
  body: string;
  recipientIds: string[];
  isActive: boolean;
  nextRunAt?: string;
  lastRunAt?: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateScheduleDto {
  name: string;
  cronExpression: string;
  subject: string;
  body: string;
  recipientIds: string[];
}

export interface UpdateScheduleDto {
  name?: string;
  cronExpression?: string;
  subject?: string;
  body?: string;
  recipientIds?: string[];
}

export interface MailboxStatsDto {
  emailsSent: number;
  scheduledEmails: number;
  contacts: number;
  successRate: number;
}

export interface GenerateEmailRequestDto {
  jobTitle: string;
  companyName: string;
  jobDescription: string;
  coverLetterHint?: string;
  candidateContext?: string;
}

export interface GenerateEmailResponseDto {
  subject: string;
  body: string;
}

export interface BulkGenerateContactItemDto {
  id: string;
  name: string;
  email: string;
  company?: string;
  position?: string;
}

export interface BulkGenerateContactRequestDto {
  contacts: BulkGenerateContactItemDto[];
  jobTitle: string;
  companyName: string;
  jobDescription: string;
  coverLetterHint?: string;
  candidateContext?: string;
}

export interface BulkGenerateContactResultDto {
  contactId: string;
  contactName: string;
  contactEmail: string;
  subject?: string;
  body?: string;
  error?: string;
}

export interface BulkGenerateContactResponseDto {
  results: BulkGenerateContactResultDto[];
  total: number;
  generated: number;
  failed: number;
}

export interface CsvImportResultDto {
  imported: number;
  skipped: number;
  invalid: number;
}
