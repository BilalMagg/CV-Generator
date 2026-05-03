import { Injectable, inject } from '@angular/core';
import { HttpService } from '../services/http.service';
import {
  ApplicationResponseDto,
  ApplicationListDto,
  ApplicationStatisticsDto,
  CreateApplicationDto,
  UpdateStatusDto,
  UpdateApplicationDto,
} from '../models/application.model';

function isApiEnvelope<T>(resp: unknown): resp is { success: boolean; data?: T } {
  return typeof resp === 'object' && resp !== null && 'success' in resp;
}

function unwrap<T>(resp: unknown): { success: boolean; data?: T } {
  if (isApiEnvelope<T>(resp)) return { success: resp.success, data: resp.data };
  return { success: true, data: resp as T };
}

@Injectable({
  providedIn: 'root',
})
export class ApplicationService {
  private http = inject(HttpService);

  async getAll(params?: {
    candidateId?: string;
    page?: number;
    pageSize?: number;
    status?: string;
    search?: string;
  }): Promise<{ success: boolean; data?: ApplicationListDto }> {
    const qs = new URLSearchParams();
    if (params?.candidateId) qs.set('candidateId', params.candidateId);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.status) qs.set('status', params.status);
    if (params?.search) qs.set('search', params.search);
    const query = qs.toString();
    const resp = await this.http.get<ApplicationListDto>(
      `/api/applications${query ? `?${query}` : ''}`,
    );
    return unwrap<ApplicationListDto>(resp);
  }

  async getById(id: string): Promise<{ success: boolean; data?: ApplicationResponseDto }> {
    const resp = await this.http.get<ApplicationResponseDto>(`/api/applications/${id}`);
    return unwrap<ApplicationResponseDto>(resp);
  }

  async create(dto: CreateApplicationDto): Promise<{ success: boolean; data?: ApplicationResponseDto }> {
    const resp = await this.http.post<ApplicationResponseDto>('/api/applications', dto);
    return unwrap<ApplicationResponseDto>(resp);
  }

  async updateStatus(id: string, dto: UpdateStatusDto): Promise<{ success: boolean; data?: ApplicationResponseDto }> {
    const resp = await this.http.patch<ApplicationResponseDto>(`/api/applications/${id}/status`, dto);
    return unwrap<ApplicationResponseDto>(resp);
  }

  async update(id: string, dto: UpdateApplicationDto): Promise<{ success: boolean; data?: ApplicationResponseDto }> {
    const resp = await this.http.put<ApplicationResponseDto>(`/api/applications/${id}`, dto);
    return unwrap<ApplicationResponseDto>(resp);
  }

  async delete(id: string): Promise<void> {
    return this.http.delete<void>(`/api/applications/${id}`);
  }

  async getStatistics(params?: {
    candidateId?: string;
  }): Promise<{ success: boolean; data?: ApplicationStatisticsDto }> {
    const query = params?.candidateId ? `?candidateId=${params.candidateId}` : '';
    const resp = await this.http.get<ApplicationStatisticsDto>(`/api/applications/statistics${query}`);
    return unwrap<ApplicationStatisticsDto>(resp);
  }
}
