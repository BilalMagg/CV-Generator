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
  /** Per-row CV version; omitted (or null) means "use the run CV". */
  cvVersionId?: string | null;
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
  cvVersionId?: string | null;
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
  cancelRequested: boolean;
  createdAt: string;
  completedAt?: string | null;
  cancelledAt?: string | null;
  rows: BulkApplyRowDto[];
}

// ── AI template mapping ─────────────────────────────────────────────────────────

/** One extracted row handed to the AI mapper (no subject/body yet). */
export interface TemplateMapRowDto {
  companyName: string;
  location?: string | null;
  specialty?: string | null;
}

/** One candidate template the AI may choose from. */
export interface TemplateMapCandidateDto {
  id: string;
  name: string;
  subjectTemplate: string;
  bodyTemplate: string;
  /** Short description of what the template is for (from its variable defaults / name). */
  intent?: string | null;
}

export interface TemplateMapRequestDto {
  rows: TemplateMapRowDto[];
  templates: TemplateMapCandidateDto[];
}

export interface TemplateMapResultDto {
  /** Aligned with the request rows: the chosen template id ('' = no match). */
  templateIds: string[];
}
