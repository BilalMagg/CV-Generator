import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';

export interface SearchAgentRequest {
  extractedJobId?: string;
  text?: string;
  keywords?: string;
}

export interface SearchOutput {
  matched_skills: any[];
  matched_experiences: any[];
  matched_projects: any[];
  gap_skills: string[];
  match_score: number;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  error?: string;
}

@Injectable({
  providedIn: 'root'
})
export class SearchAgentService {
  private readonly http = inject(HttpService);

  async search(request: SearchAgentRequest): Promise<SearchOutput> {
    const response = await this.http.post<ApiResponse<SearchOutput>>('/api/agents/search', request);
    if (!response.success || !response.data) {
      throw new Error(response.message || 'Failed to search candidates');
    }
    return response.data;
  }
}

