export interface BimeChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface BimeChatRequest {
  message: string;
  conversation_id?: string;
  provider?: string;
  model?: string;
}

export interface BimeChatResponse {
  reply: string;
  conversation_id: string;
}

export interface BimeConversationSummary {
  id: string;
  title: string;
  created_at: string;
  updated_at: string;
  message_count: number;
}

export interface BimeConversationDetail {
  id: string;
  title: string;
  messages: BimeChatMessage[];
}
