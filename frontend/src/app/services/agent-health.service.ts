import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';

export interface AgentHealthStatus {
  agentName: string;
  healthy: boolean;
  latencyMs: number;
  errorMessage?: string;
  lastChecked: string;
}

export interface HealthCheckResponse {
  success: boolean;
  message?: string;
  data?: Record<string, AgentHealthStatus>;
}

@Injectable({
  providedIn: 'root'
})
export class AgentHealthService {
  private readonly http = inject(HttpService);

  async checkHealth(): Promise<Record<string, AgentHealthStatus>> {
    const response = await this.http.get<HealthCheckResponse>('/api/workflows/agents/health');
    return response.data ?? {};
  }
}
