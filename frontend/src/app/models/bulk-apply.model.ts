// ── Bulk Apply (paste many companies → send sequentially) ───────────────────────

export interface BulkExtractRowDto {
  companyName: string;
  location?: string | null;
  email?: string | null;
  contactName?: string | null;
  specialties: string[];
}

export interface BulkExtractResultDto {
  method: 'deterministic' | 'llm';
  rows: BulkExtractRowDto[];
}

export interface BulkApplyRowInputDto {
  companyName: string;
  location?: string;
  specialty?: string;
  recipientEmail: string;
  recipientName?: string;
  subject: string;
  body: string;
}

export interface CreateBulkApplyRunDto {
  cvVersionId?: string;
  delaySeconds: number;
  rows: BulkApplyRowInputDto[];
}

export interface BulkApplySubmitResponse {
  runId: string;
  totalRows: number;
}

export type BulkRowStatus = 'pending' | 'sending' | 'sent' | 'failed' | 'skipped';

export type BulkRunStatus = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';

export interface BulkApplyRowDto {
  id: string;
  sortOrder: number;
  companyName: string;
  location?: string | null;
  specialty?: string | null;
  recipientEmail?: string | null;
  recipientName?: string | null;
  subject?: string | null;
  status: BulkRowStatus;
  errorMessage?: string | null;
  applicationId?: string | null;
  contactId?: string | null;
  companyId?: string | null;
  sentAt?: string | null;
}

export interface BulkApplyRunDto {
  id: string;
  status: BulkRunStatus;
  totalRows: number;
  processedRows: number;
  sentCount: number;
  failedCount: number;
  skippedCount: number;
  delaySeconds: number;
  cvVersionId?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  completedAt?: string | null;
  cancelledAt?: string | null;
  rows: BulkApplyRowDto[];
}
