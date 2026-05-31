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
}

export interface UpdateContactDto {
  name?: string;
  email?: string;
  phone?: string;
  company?: string;
  position?: string;
  notes?: string;
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
}

export interface EmailHistoryResponse {
  items: EmailMessageDto[];
  total: number;
  page: number;
  pageSize: number;
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
