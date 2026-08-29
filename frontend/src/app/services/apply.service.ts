import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  ApplyEmailRequest,
  ApplyEmailResult,
  ApplyPrepFormRequest,
  ApplyPrepFormResult,
  ApplyPrepMessageRequest,
  ApplyPrepMessageResult,
} from '../models/apply.model';

@Injectable({ providedIn: 'root' })
export class ApplyService {
  private readonly http = inject(HttpService);

  apply(dto: ApplyEmailRequest): Promise<ApiResponse<ApplyEmailResult>> {
    return this.http.post<ApiResponse<ApplyEmailResult>>('/api/applications/apply', dto);
  }

  generateFormResponses(dto: ApplyPrepFormRequest): Promise<ApiResponse<ApplyPrepFormResult>> {
    return this.http.post<ApiResponse<ApplyPrepFormResult>>('/api/applications/apply-prep/form-responses', dto);
  }

  generateMessage(dto: ApplyPrepMessageRequest): Promise<ApiResponse<ApplyPrepMessageResult>> {
    return this.http.post<ApiResponse<ApplyPrepMessageResult>>('/api/applications/apply-prep/message', dto);
  }
}
