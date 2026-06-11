import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';

export interface TemplateDefinition {
  id: string;
  name: string;
  previewUrl?: string;
  type: 'latex' | 'html';
  description?: string;
}

export interface TemplateRenderRequest {
  user_id: string;
  target_role: string;
  template_id?: string;
  tone?: string;
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

  async getTemplates(): Promise<TemplateDefinition[]> {
    try {
      const res = await this.http.get<ApiResponse<TemplateDefinition[]>>('/api/workflows/template/templates');
      return res.data ?? [];
    } catch {
      return [];
    }
  }

  async renderCV(params: TemplateRenderRequest): Promise<RenderedCV> {
    const res = await this.http.post<ApiResponse<RenderedCV>>('/api/workflows/template/render', params);
    if (!res.success || !res.data) throw new Error(res.message ?? 'Render failed');
    return res.data;
  }
}
