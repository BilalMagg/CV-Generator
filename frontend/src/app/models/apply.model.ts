// ── Generic schedule templates ───────────────────────────────────────────────

export interface ScheduleVariableDefaults {
  company_name?: string;
  company_description?: string;
  my_name?: string;
  my_phone?: string;
  my_email?: string;
}

export interface ScheduleAttachmentRef {
  fileName: string;
  contentType?: string;
  objectKey: string;
  sizeBytes: number;
}

export interface ScheduleTemplateDto {
  id: string;
  userId: string;
  name: string;
  subjectTemplate: string;
  bodyTemplate: string;
  variableDefaults?: ScheduleVariableDefaults | null;
  cvVersionId?: string | null;
  attachments: ScheduleAttachmentRef[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateScheduleTemplateDto {
  name: string;
  subjectTemplate: string;
  bodyTemplate: string;
  variableDefaults?: ScheduleVariableDefaults | null;
  cvVersionId?: string | null;
  attachments?: ScheduleAttachmentRef[] | null;
}

export interface UpdateScheduleTemplateDto {
  name?: string;
  subjectTemplate?: string;
  bodyTemplate?: string;
  variableDefaults?: ScheduleVariableDefaults | null;
  cvVersionId?: string | null;
  attachments?: ScheduleAttachmentRef[] | null;
}

export interface ApplyTemplateDto {
  templateId: string;
  companyId?: string;
  companyName?: string;
  companyDescription?: string;
  recipientContactId?: string;
  recipientEmail?: string;
  recipientName?: string;
  contactNotes?: string;
  cronExpression: string;
  scheduleName?: string;
  createCompanyIfMissing?: boolean;
}

export interface ApplyTemplateResultDto {
  scheduleId: string;
  applicationId: string;
  companyId: string;
  contactId: string;
}

// ── Apply wizard ──────────────────────────────────────────────────────────────

export interface ApplyEmailRequest {
  companyName: string;
  positionTitle: string;
  companyDescription?: string;
  recipientEmail: string;
  recipientName?: string;
  contactNotes?: string;
  subject: string;
  body: string;
  cvVersionId?: string;
  attachments?: EmailAttachmentPayload[];
  scheduleCron?: string;
  scheduleName?: string;
  allowDuplicate?: boolean;
}

export interface ApplyEmailResult {
  applicationId: string;
  scheduleId?: string;
  attemptId?: string;
  contactId: string;
  companyId: string;
  sentNow: boolean;
}

export interface EmailAttachmentPayload {
  fileName: string;
  contentType: string;
  contentBase64: string;
}
