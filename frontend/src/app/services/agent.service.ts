import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { Agent } from '@app/components/agents-hub/agent-card/agent-card.component';

interface AgentDto {
  id: string;
  agentId: string;
  name: string;
  role: string;
  backgroundGradient: string;
  sortOrder: number;
  isActive: boolean;
}

interface ApiResponse<T> {
  success: boolean;
  data?: T;
}

@Injectable({
  providedIn: 'root'
})
export class AgentService {
  private readonly http = inject(HttpService);

  async getAgents(): Promise<Agent[]> {
    try {
      const response = await this.http.get<ApiResponse<AgentDto[]>>('/api/agents');
      return (response.data ?? []).map(dto => ({
        id: dto.agentId,
        name: dto.name,
        role: dto.role,
        background: dto.backgroundGradient,
        status: 'active' as const,
      }));
    } catch {
      return [];
    }
  }
}
