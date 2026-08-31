import { inject, Injectable } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';

export interface AutofillField {
  name: string;
  label?: string;
  type?: string;
  options?: string[];
  help?: string;
}

export interface AutofillRequest {
  entityType: string;
  text: string;
  fields: AutofillField[];
}

export interface AutofillResult {
  values: Record<string, any>;
}

@Injectable({ providedIn: 'root' })
export class AutofillService {
  private readonly http = inject(HttpService);

  extract(request: AutofillRequest): Promise<ApiResponse<AutofillResult>> {
    return this.http.post<ApiResponse<AutofillResult>>('/api/autofill/extract', request);
  }
}
