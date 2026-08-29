import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '@env/environment';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  CvDocumentDto,
  CvTemplateDto,
  CvTemplateInput,
  CvUpdateInput,
} from '../models/document.model';

@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private readonly http = inject(HttpService);
  private readonly httpClient = inject(HttpClient);

  // Raw bytes via the app's own authenticated HTTP path (cookies + interceptors).
  // Used to render a PDF from a blob: URL — iframe navigation can hit CORS /
  // browser-PDF-viewer walls that a same-origin fetch does not.
  getVersionFileBlob(versionId: string): Promise<Blob> {
    const url = `${environment.apiUrl}/api/cv/versions/${versionId}/file`;
    return firstValueFrom(
      this.httpClient.get(url, {
        responseType: 'blob',
        withCredentials: true,
      }),
    );
  }

  // ── CVs ────────────────────────────────────────────────────────────────
  listCvs(): Promise<ApiResponse<CvDocumentDto[]>> {
    return this.http.get<ApiResponse<CvDocumentDto[]>>('/api/cv');
  }

  getCv(id: string): Promise<ApiResponse<CvDocumentDto>> {
    return this.http.get<ApiResponse<CvDocumentDto>>(`/api/cv/${id}`);
  }

  updateCv(id: string, dto: CvUpdateInput): Promise<ApiResponse<CvDocumentDto>> {
    return this.http.put<ApiResponse<CvDocumentDto>>(`/api/cv/${id}`, dto);
  }

  deleteCv(id: string): Promise<void> {
    return this.http.delete<void>(`/api/cv/${id}`);
  }

  uploadCv(file: File, title: string): Promise<ApiResponse<CvDocumentDto>> {
    const fd = new FormData();
    fd.append('file', file, file.name);
    fd.append('title', title);
    return this.http.post<ApiResponse<CvDocumentDto>>('/api/cv/upload', fd);
  }

  // Same-origin URL for inline PDF preview / download (the browser's PDF viewer
  // can't iframe the cross-origin MinIO URL directly — it needs CORS).
  versionFileUrl(versionId: string, download = false): string {
    return `${environment.apiUrl}/api/cv/versions/${versionId}/file${download ? '?download=1' : ''}`;
  }

  // Same-origin first-page PNG URL. When the version has no stored thumbnail
  // yet, this endpoint self-heals by generating one on first view (the saved
  // MinIO URL is preferred once it exists — public read, no request round-trip).
  versionThumbnailUrl(versionId: string): string {
    return `${environment.apiUrl}/api/cv/versions/${versionId}/thumbnail`;
  }

  // ── Templates ─────────────────────────────────────────────────────────
  listTemplates(): Promise<ApiResponse<CvTemplateDto[]>> {
    return this.http.get<ApiResponse<CvTemplateDto[]>>('/api/cv/templates');
  }

  createTemplate(input: CvTemplateInput): Promise<ApiResponse<CvTemplateDto>> {
    return this.http.post<ApiResponse<CvTemplateDto>>('/api/cv/templates', input);
  }

  updateTemplate(id: string, input: CvTemplateInput): Promise<ApiResponse<CvTemplateDto>> {
    return this.http.put<ApiResponse<CvTemplateDto>>(`/api/cv/templates/${id}`, input);
  }

  deleteTemplate(id: string): Promise<void> {
    return this.http.delete<void>(`/api/cv/templates/${id}`);
  }
}