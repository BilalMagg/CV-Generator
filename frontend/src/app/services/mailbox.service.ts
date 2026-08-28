import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  EmailMessageDto, SendEmailDto, SendEmailResult, EmailHistoryResponse,
  EmailScheduleDto, CreateScheduleDto, UpdateScheduleDto, ScheduleHistoryResponse,
  MailboxStatsDto, ScheduleAttachmentRef,
} from '../models/mailbox.model';
import {
  ScheduleTemplateDto, CreateScheduleTemplateDto, UpdateScheduleTemplateDto,
  ApplyTemplateDto, ApplyTemplateResultDto,
} from '../models/apply.model';

@Injectable({ providedIn: 'root' })
export class MailboxService {
  private readonly http = inject(HttpService);

  getStats(): Promise<ApiResponse<MailboxStatsDto>> {
    return this.http.get<ApiResponse<MailboxStatsDto>>('/api/mailbox/stats');
  }

  send(dto: SendEmailDto): Promise<ApiResponse<SendEmailResult>> {
    return this.http.post<ApiResponse<SendEmailResult>>('/api/mailbox/send', dto);
  }

  /** Uploads a file to MinIO for use as a schedule/template attachment. */
  uploadAttachment(file: File): Promise<ApiResponse<ScheduleAttachmentRef>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post<ApiResponse<ScheduleAttachmentRef>>('/api/mailbox/attachments', form);
  }

  getHistory(params?: { page?: number; pageSize?: number; status?: string; search?: string }): Promise<ApiResponse<EmailHistoryResponse>> {
    const qs = new URLSearchParams();
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.status) qs.set('status', params.status);
    if (params?.search) qs.set('search', params.search);
    const query = qs.toString();
    return this.http.get<ApiResponse<EmailHistoryResponse>>(`/api/mailbox/history${query ? `?${query}` : ''}`);
  }

  getHistoryDetail(id: string): Promise<ApiResponse<EmailMessageDto>> {
    return this.http.get<ApiResponse<EmailMessageDto>>(`/api/mailbox/history/${id}`);
  }

  getSchedules(): Promise<ApiResponse<EmailScheduleDto[]>> {
    return this.http.get<ApiResponse<EmailScheduleDto[]>>('/api/email-schedules');
  }

  getSchedule(scheduleId: string): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.get<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${scheduleId}`);
  }

  runScheduleNow(scheduleId: string): Promise<ApiResponse<{ sent: number; failed: number }>> {
    return this.http.post<ApiResponse<{ sent: number; failed: number }>>(`/api/email-schedules/${scheduleId}/run-now`, {});
  }

  getScheduleHistory(scheduleId: string): Promise<ApiResponse<ScheduleHistoryResponse>> {
    return this.http.get<ApiResponse<ScheduleHistoryResponse>>(`/api/email-schedules/${scheduleId}/history`);
  }

  createSchedule(dto: CreateScheduleDto): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.post<ApiResponse<EmailScheduleDto>>('/api/email-schedules', dto);
  }

  updateSchedule(scheduleId: string, dto: UpdateScheduleDto): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.put<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${scheduleId}`, dto);
  }

  deleteSchedule(scheduleId: string): Promise<void> {
    return this.http.delete<void>(`/api/email-schedules/${scheduleId}`);
  }

  toggleSchedule(scheduleId: string): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.patch<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${scheduleId}/toggle`, {});
  }

  // ── Schedule templates (reusable generic schedules) ─────────────────────────

  getScheduleTemplates(): Promise<ApiResponse<ScheduleTemplateDto[]>> {
    return this.http.get<ApiResponse<ScheduleTemplateDto[]>>('/api/email-schedules/templates');
  }

  getScheduleTemplate(id: string): Promise<ApiResponse<ScheduleTemplateDto>> {
    return this.http.get<ApiResponse<ScheduleTemplateDto>>(`/api/email-schedules/templates/${id}`);
  }

  createScheduleTemplate(dto: CreateScheduleTemplateDto): Promise<ApiResponse<ScheduleTemplateDto>> {
    return this.http.post<ApiResponse<ScheduleTemplateDto>>('/api/email-schedules/templates', dto);
  }

  updateScheduleTemplate(id: string, dto: UpdateScheduleTemplateDto): Promise<ApiResponse<ScheduleTemplateDto>> {
    return this.http.put<ApiResponse<ScheduleTemplateDto>>(`/api/email-schedules/templates/${id}`, dto);
  }

  deleteScheduleTemplate(id: string): Promise<void> {
    return this.http.delete<void>(`/api/email-schedules/templates/${id}`);
  }

  applyTemplate(dto: ApplyTemplateDto): Promise<ApiResponse<ApplyTemplateResultDto>> {
    return this.http.post<ApiResponse<ApplyTemplateResultDto>>('/api/email-schedules/apply-template', dto);
  }

  getGmailStatus(): Promise<{ connected: boolean; email?: string; connectedAt?: string }> {
    return this.http.get<{ connected: boolean; email?: string; connectedAt?: string }>('/api/gmail/status');
  }

  disconnectGmail(): Promise<void> {
    return this.http.delete<void>('/api/gmail/disconnect');
  }
}
