import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '@env/environment';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  ApplyEmailRequest,
  ApplyEmailResult,
  ApplyPrepFormRequest,
  ApplyPrepFormResult,
  ApplyPrepMessageRequest,
  ApplyPrepMessageResult,
  EmailDraftDto,
  EmailDraftListItemDto,
  SaveEmailDraftDto,
} from '../models/apply.model';

@Injectable({ providedIn: 'root' })
export class ApplyService {
  private readonly http = inject(HttpService);
  private readonly httpClient = inject(HttpClient);

  apply(dto: ApplyEmailRequest): Promise<ApiResponse<ApplyEmailResult>> {
    return this.http.post<ApiResponse<ApplyEmailResult>>('/api/applications/apply', dto);
  }

  generateFormResponses(dto: ApplyPrepFormRequest): Promise<ApiResponse<ApplyPrepFormResult>> {
    return this.http.post<ApiResponse<ApplyPrepFormResult>>('/api/applications/apply-prep/form-responses', dto);
  }

  generateMessage(dto: ApplyPrepMessageRequest): Promise<ApiResponse<ApplyPrepMessageResult>> {
    return this.http.post<ApiResponse<ApplyPrepMessageResult>>('/api/applications/apply-prep/message', dto);
  }

  // ── Reusable email drafts ───────────────────────────────────────────────────────

  listDrafts(): Promise<ApiResponse<EmailDraftListItemDto[]>> {
    return this.http.get<ApiResponse<EmailDraftListItemDto[]>>('/api/email-drafts');
  }

  saveDraft(dto: SaveEmailDraftDto): Promise<ApiResponse<EmailDraftDto>> {
    return this.http.post<ApiResponse<EmailDraftDto>>('/api/email-drafts', dto);
  }

  getDraft(id: string): Promise<ApiResponse<EmailDraftDto>> {
    return this.http.get<ApiResponse<EmailDraftDto>>(`/api/email-drafts/${id}`);
  }

  deleteDraft(id: string): Promise<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`/api/email-drafts/${id}`);
  }

  /** Raw bytes of a draft attachment via the app's own authenticated HTTP path. */
  getDraftAttachmentBlob(id: string, index: number): Promise<Blob> {
    const url = `${environment.apiUrl}/api/email-drafts/${id}/attachments/${index}/download`;
    return firstValueFrom(
      this.httpClient.get(url, { responseType: 'blob', withCredentials: true }),
    );
  }
}
