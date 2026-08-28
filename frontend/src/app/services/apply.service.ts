import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import { ApplyEmailRequest, ApplyEmailResult } from '../models/apply.model';

@Injectable({ providedIn: 'root' })
export class ApplyService {
  private readonly http = inject(HttpService);

  apply(dto: ApplyEmailRequest): Promise<ApiResponse<ApplyEmailResult>> {
    return this.http.post<ApiResponse<ApplyEmailResult>>('/api/applications/apply', dto);
  }
}
