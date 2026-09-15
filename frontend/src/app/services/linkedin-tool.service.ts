import { inject, Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  CreateSavedToolContent,
  EmojifyRequest,
  EmojifyResult,
  LinkedInRequest,
  LinkedInResult,
  SavedToolContent,
  UpdateSavedToolContent,
} from '../models/tools.model';

@Injectable({ providedIn: 'root' })
export class LinkedInToolService {
  private readonly http = inject(HttpService);

  generate(request: LinkedInRequest): Promise<ApiResponse<LinkedInResult>> {
    return this.http.post<ApiResponse<LinkedInResult>>('/api/direct-ai/linkedin', request);
  }

  emojify(request: EmojifyRequest): Promise<ApiResponse<EmojifyResult>> {
    return this.http.post<ApiResponse<EmojifyResult>>('/api/direct-ai/emojify', request);
  }

  listSaved(): Promise<ApiResponse<SavedToolContent[]>> {
    return this.http.get<ApiResponse<SavedToolContent[]>>('/api/direct-ai/saved');
  }

  createSaved(payload: CreateSavedToolContent): Promise<ApiResponse<SavedToolContent>> {
    return this.http.post<ApiResponse<SavedToolContent>>('/api/direct-ai/saved', payload);
  }

  updateSaved(id: string, payload: UpdateSavedToolContent): Promise<ApiResponse<SavedToolContent>> {
    return this.http.put<ApiResponse<SavedToolContent>>(`/api/direct-ai/saved/${id}`, payload);
  }

  revertSaved(id: string): Promise<ApiResponse<SavedToolContent>> {
    return this.http.post<ApiResponse<SavedToolContent>>(`/api/direct-ai/saved/${id}/revert`, {});
  }

  deleteSaved(id: string): Promise<ApiResponse<null>> {
    return this.http.delete<ApiResponse<null>>(`/api/direct-ai/saved/${id}`);
  }
}