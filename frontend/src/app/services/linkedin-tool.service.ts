import { inject, Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import { LinkedInRequest, LinkedInResult } from '../models/tools.model';

@Injectable({ providedIn: 'root' })
export class LinkedInToolService {
  private readonly http = inject(HttpService);

  generate(request: LinkedInRequest): Promise<ApiResponse<LinkedInResult>> {
    return this.http.post<ApiResponse<LinkedInResult>>('/api/direct-ai/linkedin', request);
  }
}