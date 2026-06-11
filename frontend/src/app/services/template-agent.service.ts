import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';

export interface TemplateRenderRequest {
  user_id: string;
  target_role: string;
  template_id?: string;
  summary?: string;
}

export interface RenderedCV {
  cv_code: string;
  template_id: string;
  sections?: string[];
  pdf_url?: string;
  code_url?: string;
}

interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
}

@Injectable({ providedIn: 'root' })
export class TemplateAgentService {
  private readonly http = inject(HttpService);

  async renderCV(params: TemplateRenderRequest): Promise<RenderedCV> {
    const res = await this.http.post<ApiResponse<RenderedCV>>('/api/workflows/template/render', params);
    if (!res.success || !res.data) throw new Error(res.message ?? 'Render failed');
    return res.data;
  }
}
