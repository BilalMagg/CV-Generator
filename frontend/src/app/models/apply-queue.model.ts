/** One staged apply waiting in the Queue to be executed as a bulk-apply run. */
export interface ApplyQueueItemDto {
  id: string;
  source: 'bulk_apply' | 'quick_apply';
  applicationId: string;
  applicationStatus?: string | null;
  contactId?: string | null;
  companyId: string;
  companyName: string;
  location?: string | null;
  positionTitle?: string | null;
  recipientEmail: string;
  recipientName?: string | null;
  subject: string;
  body?: string | null;
  cvVersionId?: string | null;
  /** pending | queued | sent | failed | removed */
  status: string;
  bulkRunId?: string | null;
  errorMessage?: string | null;
  createdAt: string;
  sentAt?: string | null;
}

export interface SaveApplyQueueItemDto {
  companyName: string;
  companyDescription?: string;
  location?: string;
  positionTitle?: string;
  recipientEmail: string;
  recipientName?: string;
  contactNotes?: string;
  subject: string;
  body?: string;
  cvVersionId?: string;
  source: 'bulk_apply' | 'quick_apply';
}

export interface ExecuteApplyQueueResult {
  runId: string;
  totalRows: number;
  skippedNoEmail: number;
}
