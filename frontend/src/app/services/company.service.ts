import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse, ApplicationResponseDto } from '../models/application.model';
import { ContactDto } from '@app/models/mailbox.model';

export interface CompanyDto {
  id: string;
  userId: string;
  name: string;
  websiteUrl?: string | null;
  location?: string | null;
  country?: string | null;
  locationUrl?: string | null;
  note?: string | null;
  applicationsCount?: number;
  lastAppliedAt?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCompanyDto {
  name: string;
  websiteUrl?: string;
  location?: string;
  country?: string;
  locationUrl?: string;
  note?: string;
}

export interface UpdateCompanyDto {
  name?: string;
  websiteUrl?: string;
  location?: string;
  country?: string;
  locationUrl?: string;
  note?: string;
}

export interface CompanyListResponse {
  items: CompanyDto[];
  total: number;
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly http = inject(HttpService);

  getCompanies(params?: {
    search?: string;
    country?: string;
    city?: string;
    minApps?: number;
    sortBy?: 'name' | 'apps' | 'applied';
    sortDir?: 'asc' | 'desc';
    page?: number;
    pageSize?: number;
  }): Promise<ApiResponse<CompanyListResponse>> {
    const qs = new URLSearchParams();
    if (params?.search) qs.set('search', params.search);
    if (params?.country) qs.set('country', params.country);
    if (params?.city) qs.set('city', params.city);
    if (params?.minApps !== undefined) qs.set('minApps', String(params.minApps));
    if (params?.sortBy) qs.set('sortBy', params.sortBy);
    if (params?.sortDir) qs.set('sortDir', params.sortDir);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    const query = qs.toString();
    return this.http.get<ApiResponse<CompanyListResponse>>(`/api/companies${query ? `?${query}` : ''}`);
  }

  getCompany(id: string): Promise<ApiResponse<CompanyDto>> {
    return this.http.get<ApiResponse<CompanyDto>>(`/api/companies/${id}`);
  }

  createCompany(dto: CreateCompanyDto): Promise<ApiResponse<CompanyDto>> {
    return this.http.post<ApiResponse<CompanyDto>>('/api/companies', dto);
  }

  updateCompany(id: string, dto: UpdateCompanyDto): Promise<ApiResponse<CompanyDto>> {
    return this.http.put<ApiResponse<CompanyDto>>(`/api/companies/${id}`, dto);
  }

  deleteCompany(id: string): Promise<void> {
    return this.http.delete<void>(`/api/companies/${id}`);
  }

  getCompanyApplications(id: string): Promise<ApiResponse<ApplicationResponseDto[]>> {
    return this.http.get<ApiResponse<ApplicationResponseDto[]>>(`/api/companies/${id}/applications`);
  }

  getCompanyContacts(id: string): Promise<ApiResponse<ContactDto[]>> {
    return this.http.get<ApiResponse<ContactDto[]>>(`/api/companies/${id}/contacts`);
  }
}
