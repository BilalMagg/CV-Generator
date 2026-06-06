import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ExtractorOutput, ExtractionHistoryItem, AgentConfig, DEFAULT_CONFIG } from '@app/models/extraction.types';

interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
}

interface ExtractionResponse {
  id: string;
  output: ExtractorOutput;
}

@Injectable({
  providedIn: 'root'
})
export class ExtractionService {
  private readonly http = inject(HttpService);

  async extract(input: { text?: string; file?: File; url?: string; jobOfferId?: string; language: string }): Promise<{ id: string; output: ExtractorOutput }> {
    let text = input.text;

    if (input.file) {
      text = await input.file.text();
    }

    const body: Record<string, string> = { language: input.language };
    if (text) body['text'] = text;
    if (input.url) body['url'] = input.url;
    if (input.jobOfferId) body['jobOfferId'] = input.jobOfferId;

    const response = await this.http.post<ApiResponse<ExtractionResponse>>('/api/workflows/job-extractions', body);
    if (!response.success || !response.data) throw new Error(response.message || 'Extraction failed');
    return { id: response.data.id, output: response.data.output };
  }

  async getExtraction(id: string): Promise<ExtractorOutput> {
    const response = await this.http.get<ApiResponse<ExtractionResponse>>(`/api/workflows/job-extractions/${id}`);
    if (!response.success || !response.data) throw new Error(response.message || 'Extraction not found');
    return response.data.output;
  }

  async getHistory(agentId: string): Promise<ExtractionHistoryItem[]> {
    try {
      const response = await this.http.get<ApiResponse<ExtractionHistoryItem[]>>('/api/workflows/job-extractions');
      return response.data ?? [];
    } catch {
      return [];
    }
  }

  async getConfig(agentId: string): Promise<AgentConfig> {
    const stored = localStorage.getItem(`agent-config-${agentId}`);
    return stored ? JSON.parse(stored) : { ...DEFAULT_CONFIG };
  }

  async saveConfig(agentId: string, config: AgentConfig): Promise<void> {
    localStorage.setItem(`agent-config-${agentId}`, JSON.stringify(config));
  }
}
