import { Injectable, inject } from '@angular/core';
import { HttpService } from '../services/http.service';
import {
  ApplicationResponseDto,
  ApplicationListDto,
  ApplicationStatisticsDto,
  StatisticsTrendsDto,
  ActivityFeedDto,
  CreateApplicationDto,
  UpdateStatusDto,
  UpdateApplicationDto,
  ApiResponse,
} from '../models/application.model';

@Injectable({
  providedIn: 'root',
})
export class ApplicationService {
  private http = inject(HttpService);

  async getAll(params?: {
    candidateId?: string;
    page?: number;
    pageSize?: number;
    statuses?: string[];
    search?: string;
    appliedFrom?: string;
    appliedTo?: string;
    updatedFrom?: string;
    updatedTo?: string;
  }): Promise<ApiResponse<ApplicationListDto>> {
    const qs = new URLSearchParams();
    if (params?.candidateId) qs.set('candidateId', params.candidateId);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.statuses?.length) qs.set('statuses', params.statuses.join(','));
    if (params?.search) qs.set('search', params.search);
    if (params?.appliedFrom) qs.set('appliedFrom', params.appliedFrom);
    if (params?.appliedTo) qs.set('appliedTo', params.appliedTo);
    if (params?.updatedFrom) qs.set('updatedFrom', params.updatedFrom);
    if (params?.updatedTo) qs.set('updatedTo', params.updatedTo);
    const query = qs.toString();
    return this.http.get<ApiResponse<ApplicationListDto>>(
      `/api/applications${query ? `?${query}` : ''}`,
    );
  }

  async getById(id: string): Promise<ApiResponse<ApplicationResponseDto>> {
    return this.http.get<ApiResponse<ApplicationResponseDto>>(`/api/applications/${id}`);
  }

  async create(dto: CreateApplicationDto): Promise<ApiResponse<ApplicationResponseDto>> {
    return this.http.post<ApiResponse<ApplicationResponseDto>>('/api/applications', dto);
  }

  async updateStatus(id: string, dto: UpdateStatusDto): Promise<ApiResponse<ApplicationResponseDto>> {
    return this.http.patch<ApiResponse<ApplicationResponseDto>>(`/api/applications/${id}/status`, dto);
  }

  async update(id: string, dto: UpdateApplicationDto): Promise<ApiResponse<ApplicationResponseDto>> {
    return this.http.put<ApiResponse<ApplicationResponseDto>>(`/api/applications/${id}`, dto);
  }

  async delete(id: string): Promise<void> {
    return this.http.delete<void>(`/api/applications/${id}`);
  }

  async getStatistics(params?: {
    candidateId?: string;
  }): Promise<ApiResponse<ApplicationStatisticsDto>> {
    const query = params?.candidateId ? `?candidateId=${params.candidateId}` : '';
    return this.http.get<ApiResponse<ApplicationStatisticsDto>>(`/api/applications/statistics${query}`);
  }

  async getTrends(params?: {
    candidateId?: string;
  }): Promise<ApiResponse<StatisticsTrendsDto>> {
    const query = params?.candidateId ? `?candidateId=${params.candidateId}` : '';
    return this.http.get<ApiResponse<StatisticsTrendsDto>>(`/api/applications/statistics/trends${query}`);
  }

  async toggleSave(id: string): Promise<ApiResponse<boolean>> {
    return this.http.patch<ApiResponse<boolean>>(`/api/applications/${id}/toggle-save`, {});
  }

  async getActivity(params?: {
    candidateId?: string;
    limit?: number;
  }): Promise<ApiResponse<ActivityFeedDto>> {
    const qs = new URLSearchParams();
    if (params?.limit) qs.set('limit', String(params.limit));
    const query = qs.toString();
    return this.http.get<ApiResponse<ActivityFeedDto>>(`/api/applications/activity${query ? `?${query}` : ''}`);
  }
}
