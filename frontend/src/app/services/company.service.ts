import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse, ApplicationResponseDto } from '../models/application.model';
import { ContactDto } from '@app/models/mailbox.model';

export interface CompanySocialLink {
  key: string;
  url: string;
}

export interface CompanyDto {
  id: string;
  userId: string;
  name: string;
  websiteUrl?: string | null;
  location?: string | null;
  country?: string | null;
  locationUrl?: string | null;
  region?: string | null;
  sector?: string | null;
  foundedYear?: number | null;
  linkedInUrl?: string | null;
  size?: string | null;
  logoUrl?: string | null;
  logoImageId?: string | null;
  note?: string | null;
  description?: string | null;
  address?: string | null;
  emails?: string[];
  phones?: string[];
  socialLinks?: CompanySocialLink[];
  companyFacts?: string[];
  researchSource?: string | null;
  researchLink?: string | null;
  researchUpdatedAt?: string | null;
  applicationsCount?: number;
  ContactsCount?: number;
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
  region?: string;
  sector?: string;
  foundedYear?: number;
  linkedInUrl?: string;
  size?: string;
  specialties?: string;
  logoUrl?: string;
  logoImageId?: string;
  note?: string;
  description?: string;
}

export interface UpdateCompanyDto {
  name?: string;
  websiteUrl?: string;
  location?: string;
  country?: string;
  locationUrl?: string;
  region?: string;
  sector?: string;
  foundedYear?: number;
  linkedInUrl?: string;
  size?: string;
  logoUrl?: string;
  logoImageId?: string;
  note?: string;
  description?: string;
}

export interface CompanyListResponse {
  items: CompanyDto[];
  total: number;
}

export interface CompanyCredentialDto {
  id: string;
  companyId: string;
  label?: string | null;
  url?: string | null;
  username?: string | null;
  password?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertCompanyCredentialDto {
  label?: string;
  url?: string;
  username?: string;
  password?: string;
  notes?: string;
}

export interface DuplicateApplicationPairDto {
  applicationA: ApplicationResponseDto;
  applicationB: ApplicationResponseDto;
  reason: string;
}

export interface DuplicateContactPairDto {
  contactA: ContactDto;
  contactB: ContactDto;
  reason: string;
}

export interface CompanyMergePreviewDto {
  companyA: CompanyDto;
  companyB: CompanyDto;
  applicationsA: ApplicationResponseDto[];
  applicationsB: ApplicationResponseDto[];
  duplicateApplications: DuplicateApplicationPairDto[];
  contactsA: ContactDto[];
  contactsB: ContactDto[];
  duplicateContacts: DuplicateContactPairDto[];
  credentialsA: CompanyCredentialDto[];
  credentialsB: CompanyCredentialDto[];
  oldestCreatedAt: string;
}

export interface MergeContactResolutionDto {
  mode: 'KeepA' | 'KeepB' | 'KeepBoth';
  contactAId: string;
  contactBId: string;
}

export interface MergeApplicationResolutionDto {
  mode: 'Merge' | 'KeepBoth';
  primaryApplicationId: string;
  secondaryApplicationId: string;
}

/** A suggested near-duplicate pair (e.g. "Inetum" / "Inetum Maroc" created by extraction). */
export interface CompanyMergeCandidateDto {
  idA: string;
  idB: string;
  nameA: string;
  nameB: string;
  /** "same-base" | "prefix-match" */
  reason: string;
  appsA: number;
  appsB: number;
  createdA: string;
  createdB: string;
}

export interface CompanyMergeRequestDto {
  sourceCompanyId: string;
  targetCompanyId: string;
  finalName: string;
  websiteUrl?: string | null;
  location?: string | null;
  country?: string | null;
  locationUrl?: string | null;
  region?: string | null;
  sector?: string | null;
  foundedYear?: number | null;
  linkedInUrl?: string | null;
  size?: string | null;
  specialties?: string | null;
  logoImageId?: string | null;
  logoUrl?: string | null;
  description?: string | null;
  note?: string | null;
  keepCredentialIds?: string[];
  contactResolutions?: MergeContactResolutionDto[];
  applicationResolutions?: MergeApplicationResolutionDto[];
}

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly http = inject(HttpService);

  getCompanies(params?: {
    search?: string;
    country?: string;
    city?: string;
    sector?: string;
    hasWebsite?: boolean;
    researched?: boolean;
    minApps?: number;
    sortBy?: 'name' | 'apps' | 'applied' | 'researched';
    sortDir?: 'asc' | 'desc';
    page?: number;
    pageSize?: number;
  }): Promise<ApiResponse<CompanyListResponse>> {
    const qs = new URLSearchParams();
    if (params?.search) qs.set('search', params.search);
    if (params?.country) qs.set('country', params.country);
    if (params?.city) qs.set('city', params.city);
    if (params?.sector) qs.set('sector', params.sector);
    if (params?.hasWebsite !== undefined) qs.set('hasWebsite', params.hasWebsite ? 'true' : 'false');
    if (params?.researched !== undefined) qs.set('researched', params.researched ? 'true' : 'false');
    if (params?.minApps !== undefined) qs.set('minApps', String(params.minApps));
    if (params?.sortBy) qs.set('sortBy', params.sortBy);
    if (params?.sortDir) qs.set('sortDir', params.sortDir);
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    const query = qs.toString();
    return this.http.get<ApiResponse<CompanyListResponse>>(`/api/companies${query ? `?${query}` : ''}`);
  }

  getCompanySectors(): Promise<ApiResponse<string[]>> {
    return this.http.get<ApiResponse<string[]>>('/api/companies/sectors');
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

  getCompanyCredentials(id: string): Promise<ApiResponse<CompanyCredentialDto[]>> {
    return this.http.get<ApiResponse<CompanyCredentialDto[]>>(`/api/companies/${id}/credentials`);
  }

  createCompanyCredential(companyId: string, dto: UpsertCompanyCredentialDto): Promise<ApiResponse<CompanyCredentialDto>> {
    return this.http.post<ApiResponse<CompanyCredentialDto>>(`/api/companies/${companyId}/credentials`, dto);
  }

  updateCompanyCredential(companyId: string, credentialId: string, dto: UpsertCompanyCredentialDto): Promise<ApiResponse<CompanyCredentialDto>> {
    return this.http.put<ApiResponse<CompanyCredentialDto>>(`/api/companies/${companyId}/credentials/${credentialId}`, dto);
  }

  deleteCompanyCredential(companyId: string, credentialId: string): Promise<void> {
    return this.http.delete<void>(`/api/companies/${companyId}/credentials/${credentialId}`);
  }

  getMergePreview(companyAId: string, companyBId: string): Promise<ApiResponse<CompanyMergePreviewDto>> {
    return this.http.get<ApiResponse<CompanyMergePreviewDto>>(`/api/companies/merge/preview?companyAId=${encodeURIComponent(companyAId)}&companyBId=${encodeURIComponent(companyBId)}`);
  }

  /** Suggested near-duplicate pairs, newest-pair-first relevance handled server-side. */
  getMergeCandidates(): Promise<ApiResponse<CompanyMergeCandidateDto[]>> {
    return this.http.get<ApiResponse<CompanyMergeCandidateDto[]>>('/api/companies/merge/candidates');
  }

  mergeCompanies(dto: CompanyMergeRequestDto): Promise<ApiResponse<CompanyDto>> {
    return this.http.post<ApiResponse<CompanyDto>>('/api/companies/merge', dto);
  }
}
