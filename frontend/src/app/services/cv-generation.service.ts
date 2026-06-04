import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { AuthService } from './auth.service';
import {
  ApiResponse,
  CvGenerationStatus,
  CvGenerationResult,
  CvGenerationSubmitResponse,
  CVProfile,
} from '@app/models/cv-generation.models';

@Injectable({ providedIn: 'root' })
export class CvGenerationService {
  private http = inject(HttpService);
  private auth = inject(AuthService);

  async submit(params: {
    jobDescription: string;
    candidateName?: string;
    recipientEmail?: string;
    templateId?: string;
    language?: string;
    tone?: string;
    emailSubject?: string;
    url?: string;
  }): Promise<string> {
    const userId = this.auth.currentUser()?.userId;
    const body: Record<string, any> = {
      user_id: userId,
      job_description: params.jobDescription,
    };
    if (params.candidateName) body['candidate_name'] = params.candidateName;
    if (params.recipientEmail) body['recipient_email'] = params.recipientEmail;
    if (params.templateId) body['template_id'] = params.templateId;
    if (params.language) body['language'] = params.language;
    if (params.tone) body['tone'] = params.tone;
    if (params.emailSubject) body['email_subject'] = params.emailSubject;

    const res = await this.http.post<ApiResponse<CvGenerationSubmitResponse>>(
      '/api/workflows/generate-cv',
      body,
    );
    return res.data!.run_id;
  }

  async getStatus(runId: string): Promise<CvGenerationStatus> {
    const res = await this.http.get<ApiResponse<CvGenerationStatus>>(
      `/api/workflows/generate-cv/${runId}/status`,
    );
    return res.data!;
  }

  async getResult(runId: string): Promise<CvGenerationResult> {
    const res = await this.http.get<ApiResponse<CvGenerationResult>>(
      `/api/workflows/generate-cv/${runId}/result`,
    );
    return res.data!;
  }

  async cancel(runId: string): Promise<void> {
    await this.http.post<ApiResponse<any>>(
      `/api/workflows/generate-cv/${runId}/cancel`,
      {},
    );
  }

  async fetchProfiles(): Promise<CVProfile[]> {
    try {
      const res = await this.http.get<ApiResponse<CVProfile[]>>('/api/user-content/cvprofiles');
      return res.data ?? [];
    } catch {
      return [];
    }
  }
}
