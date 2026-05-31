import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  ContactDto, CreateContactDto, UpdateContactDto, ContactListResponse,
  EmailMessageDto, SendEmailDto, EmailHistoryResponse,
  EmailScheduleDto, CreateScheduleDto, UpdateScheduleDto,
  MailboxStatsDto,
} from '../models/mailbox.model';

@Injectable({ providedIn: 'root' })
export class MailboxService {
  private readonly http = inject(HttpService);

  getStats(userId: string): Promise<ApiResponse<MailboxStatsDto>> {
    return this.http.get<ApiResponse<MailboxStatsDto>>(`/api/mailbox/${userId}/stats`);
  }

  send(userId: string, dto: SendEmailDto): Promise<ApiResponse<EmailMessageDto>> {
    return this.http.post<ApiResponse<EmailMessageDto>>(`/api/mailbox/${userId}/send`, dto);
  }

  getHistory(userId: string, params?: { page?: number; pageSize?: number; status?: string }): Promise<ApiResponse<EmailHistoryResponse>> {
    const qs = new URLSearchParams();
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.status) qs.set('status', params.status);
    const query = qs.toString();
    return this.http.get<ApiResponse<EmailHistoryResponse>>(`/api/mailbox/${userId}/history${query ? `?${query}` : ''}`);
  }

  getContacts(userId: string, params?: { page?: number; pageSize?: number; search?: string }): Promise<ApiResponse<ContactListResponse>> {
    const qs = new URLSearchParams();
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.search) qs.set('search', params.search);
    const query = qs.toString();
    return this.http.get<ApiResponse<ContactListResponse>>(`/api/contacts/${userId}${query ? `?${query}` : ''}`);
  }

  createContact(userId: string, dto: CreateContactDto): Promise<ApiResponse<ContactDto>> {
    return this.http.post<ApiResponse<ContactDto>>(`/api/contacts/${userId}`, dto);
  }

  updateContact(userId: string, contactId: string, dto: UpdateContactDto): Promise<ApiResponse<ContactDto>> {
    return this.http.put<ApiResponse<ContactDto>>(`/api/contacts/${userId}/${contactId}`, dto);
  }

  deleteContact(userId: string, contactId: string): Promise<void> {
    return this.http.delete<void>(`/api/contacts/${userId}/${contactId}`);
  }

  importCsv(userId: string, csvContent: string): Promise<ApiResponse<ContactDto[]>> {
    return this.http.post<ApiResponse<ContactDto[]>>(`/api/contacts/${userId}/import`, { csvContent });
  }

  importFromOffers(userId: string): Promise<ApiResponse<ContactDto[]>> {
    return this.http.post<ApiResponse<ContactDto[]>>(`/api/contacts/${userId}/import-from-offers`, {});
  }

  getSchedules(userId: string): Promise<ApiResponse<EmailScheduleDto[]>> {
    return this.http.get<ApiResponse<EmailScheduleDto[]>>(`/api/email-schedules/${userId}`);
  }

  createSchedule(userId: string, dto: CreateScheduleDto): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.post<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${userId}`, dto);
  }

  updateSchedule(userId: string, scheduleId: string, dto: UpdateScheduleDto): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.put<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${userId}/${scheduleId}`, dto);
  }

  deleteSchedule(userId: string, scheduleId: string): Promise<void> {
    return this.http.delete<void>(`/api/email-schedules/${userId}/${scheduleId}`);
  }

  toggleSchedule(userId: string, scheduleId: string): Promise<ApiResponse<EmailScheduleDto>> {
    return this.http.patch<ApiResponse<EmailScheduleDto>>(`/api/email-schedules/${userId}/${scheduleId}/toggle`, {});
  }
}
