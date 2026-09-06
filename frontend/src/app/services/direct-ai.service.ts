import { inject, Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';

export interface DirectMessageRequest {
  channel?: string;
  jobRole?: string;
  companyName?: string;
  jobDescription?: string;
  requiredSkills?: string[];
  responsibilities?: string[];
  contactType?: string;
  recipientName?: string;
  candidateContext?: string;
  considerations?: string;
  language?: string;
  provider?: string;
  model?: string;
}

export interface DirectMessageResult {
  subject?: string;
  message: string;
  channel?: string;
  language?: string;
}

export interface DirectChatRequest {
  system?: string;
  user: string;
  temperature?: number;
  maxTokens?: number;
  provider?: string;
  model?: string;
}

export interface DirectChatResult {
  text: string;
}

@Injectable({ providedIn: 'root' })
export class DirectAiService {
  private readonly http = inject(HttpService);

  generateMessage(request: DirectMessageRequest): Promise<ApiResponse<DirectMessageResult>> {
    return this.http.post<ApiResponse<DirectMessageResult>>('/api/direct-ai/message', request);
  }

  chat(request: DirectChatRequest): Promise<ApiResponse<DirectChatResult>> {
    return this.http.post<ApiResponse<DirectChatResult>>('/api/direct-ai/chat', request);
  }
}
