import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  EmailMessageDto, SendEmailDto, EmailHistoryResponse,
  EmailScheduleDto, CreateScheduleDto, UpdateScheduleDto,
  MailboxStatsDto,
} from '../models/mailbox.model';

@Injectable({ providedIn: 'root' })
export class MailboxService {
  private readonly http = inject(HttpService);

  getStats(): Promise<ApiResponse<MailboxStatsDto>> {
    return this.http.get<ApiResponse<MailboxStatsDto>>('/api/mailbox/stats');
  }

  send(dto: SendEmailDto): Promise<ApiResponse<EmailMessageDto>> {
    return this.http.post<ApiResponse<EmailMessageDto>>('/api/mailbox/send', dto);
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

  getGmailStatus(): Promise<{ connected: boolean; email?: string; connectedAt?: string }> {
    return this.http.get<{ connected: boolean; email?: string; connectedAt?: string }>('/api/gmail/status');
  }

  disconnectGmail(): Promise<void> {
    return this.http.delete<void>('/api/gmail/disconnect');
  }
}
