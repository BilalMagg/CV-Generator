import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { AuthService } from './auth.service';
import { ApiResponse } from '@app/models/cv-generation.models';
import {
  TemplateRenderStatus,
  TemplateRenderResult,
  TemplateRenderSubmitResponse,
  TemplateRenderSseEvent,
} from '@app/models/template-render.models';

export interface StreamHandlers {
  onSnapshot: (snap: Extract<TemplateRenderSseEvent, { type: 'snapshot' }>) => void;
  onDone: (done: Extract<TemplateRenderSseEvent, { type: 'done' }>) => void;
  onError?: (message: string) => void;
  onOpen?: () => void;
}

@Injectable({ providedIn: 'root' })
export class TemplateRenderService {
  private http = inject(HttpService);
  private auth = inject(AuthService);

  async submit(params: {
    extractionId: string;
    templateId?: string;
    language?: string;
    tone?: string;
    saveToDocuments?: boolean;
    title?: string;
  }): Promise<string> {
    const userId = this.auth.currentUser()?.userId;
    const body: Record<string, any> = {
      user_id: userId,
      extraction_id: params.extractionId,
    };
    if (params.templateId) body['template_id'] = params.templateId;
    if (params.language) body['language'] = params.language;
    if (params.tone) body['tone'] = params.tone;
    if (params.saveToDocuments !== undefined) body['save_to_documents'] = params.saveToDocuments;
    if (params.title) body['title'] = params.title;

    const res = await this.http.post<ApiResponse<TemplateRenderSubmitResponse>>(
      '/api/workflows/template-renders',
      body,
    );
    if (!res.data?.run_id) throw new Error('Failed to start template render');
    return res.data.run_id;
  }

  async getStatus(runId: string): Promise<TemplateRenderStatus> {
    const res = await this.http.get<ApiResponse<TemplateRenderStatus>>(
      `/api/workflows/template-renders/${runId}/status`,
    );
    if (!res.data) throw new Error('Status not available');
    return res.data;
  }

  async getResult(runId: string): Promise<TemplateRenderResult> {
    const res = await this.http.get<ApiResponse<TemplateRenderResult>>(
      `/api/workflows/template-renders/${runId}/result`,
    );
    if (!res.data) throw new Error('Result not available');
    return res.data;
  }

  async cancel(runId: string): Promise<void> {
    await this.http.post<ApiResponse<any>>(`/api/workflows/template-renders/${runId}/cancel`, {});
  }

  /** Opens an EventSource; returns a cleanup function that closes it. */
  streamEvents(runId: string, handlers: StreamHandlers): () => void {
    let closed = false;
    const source = new EventSource(`/api/workflows/template-renders/${runId}/events`);

    source.onopen = () => handlers.onOpen?.();

    source.onmessage = (ev) => {
      let data: TemplateRenderSseEvent;
      try {
        data = JSON.parse(ev.data);
      } catch {
        return;
      }
      if (data.type === 'snapshot') {
        handlers.onSnapshot(data);
      } else if (data.type === 'done') {
        handlers.onDone(data);
        source.close();
        closed = true;
      } else if (data.type === 'error') {
        handlers.onError?.(data.error_message ?? 'Unknown stream error');
        source.close();
        closed = true;
      }
    };

    source.onerror = () => {
      // EventSource auto-reconnects on transient failures; only a terminal
      // server event closes the stream. Guard against double-close.
      if (closed) source.close();
    };

    return () => {
      closed = true;
      source.close();
    };
  }
}