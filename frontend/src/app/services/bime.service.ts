import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import type { BimeChatRequest, BimeChatResponse, BimeConversationSummary, BimeConversationDetail } from '../models/bime.model';

@Injectable({ providedIn: 'root' })
export class BimeService {
  private readonly http = inject(HttpService);

  async chat(message: string, conversationId?: string, provider?: string, model?: string): Promise<BimeChatResponse> {
    const body: BimeChatRequest = { message };
    if (conversationId) body.conversation_id = conversationId;
    if (provider) body.provider = provider;
    if (model) body.model = model;
    const res = await this.http.post<{ success: boolean; data: BimeChatResponse }>('/api/bime/chat', body);
    return res.data;
  }

  async listConversations(): Promise<BimeConversationSummary[]> {
    const res = await this.http.get<{ success: boolean; data: BimeConversationSummary[] }>('/api/bime/conversations');
    return res.data ?? [];
  }

  async getConversation(id: string): Promise<BimeConversationDetail> {
    const res = await this.http.get<{ success: boolean; data: BimeConversationDetail }>(`/api/bime/conversations/${id}`);
    return res.data;
  }

  async deleteConversation(id: string): Promise<void> {
    await this.http.delete(`/api/bime/conversations/${id}`);
  }
}
