import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  BulkApplyRunDto,
  BulkApplySubmitResponse,
  BulkExtractResultDto,
  CreateBulkApplyRunDto,
  TemplateMapRequestDto,
  TemplateMapResultDto,
} from '../models/bulk-apply.model';

@Injectable({ providedIn: 'root' })
export class BulkApplyService {
  private readonly http = inject(HttpService);

  extract(text: string): Promise<ApiResponse<BulkExtractResultDto>> {
    return this.http.post<ApiResponse<BulkExtractResultDto>>('/api/applications/bulk-apply/extract', { text });
  }

  mapTemplates(dto: TemplateMapRequestDto): Promise<ApiResponse<TemplateMapResultDto>> {
    return this.http.post<ApiResponse<TemplateMapResultDto>>(
      '/api/applications/bulk-apply/map-templates',
      dto,
    );
  }

  createRun(dto: CreateBulkApplyRunDto): Promise<ApiResponse<BulkApplySubmitResponse>> {
    return this.http.post<ApiResponse<BulkApplySubmitResponse>>('/api/applications/bulk-apply', dto);
  }

  getRun(id: string): Promise<ApiResponse<BulkApplyRunDto>> {
    return this.http.get<ApiResponse<BulkApplyRunDto>>(`/api/applications/bulk-apply/${id}`);
  }

  cancel(id: string): Promise<ApiResponse<unknown>> {
    return this.http.post<ApiResponse<unknown>>(`/api/applications/bulk-apply/${id}/cancel`, {});
  }

  retryFailed(id: string): Promise<ApiResponse<BulkApplyRunDto>> {
    return this.http.post<ApiResponse<BulkApplyRunDto>>(`/api/applications/bulk-apply/${id}/retry`, {});
  }
}
