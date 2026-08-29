import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import type {
  LlmProviderInfo,
  LlmProvidersResponse,
  LlmModelsResponse,
  LlmSettings,
  LlmSettingsRequest,
  AgentLlmSettingsDto,
  AgentLlmSettingMapDto,
  AgentLlmSettingDto,
} from '../models/llm-settings.model';

interface Envelope<T> {
  success: boolean;
  data?: T;
}

@Injectable({ providedIn: 'root' })
export class LlmSettingsService {
  private readonly http = inject(HttpService);

  private modelsCache = new Map<string, { fetchedAt: number; models: string[] }>();
  private static readonly MODELS_TTL_MS = 10 * 60 * 1000;

  async getSettings(): Promise<LlmSettings | null> {
    const res = await this.http.get<Envelope<LlmSettings | null>>('/api/llm-settings');
    return res.data ?? null;
  }

  async setSettings(provider: string, model: string): Promise<LlmSettings> {
    const body: LlmSettingsRequest = { provider, model };
    const res = await this.http.put<Envelope<LlmSettings>>('/api/llm-settings', body);
    return res.data!;
  }

  async getProviders(): Promise<LlmProviderInfo[]> {
    const res = await this.http.get<Envelope<LlmProvidersResponse>>('/api/llm-settings/providers');
    return res.data?.providers ?? [];
  }

  async getModels(provider: string): Promise<string[]> {
    const cached = this.modelsCache.get(provider);
    if (cached && Date.now() - cached.fetchedAt < LlmSettingsService.MODELS_TTL_MS) {
      return cached.models;
    }
    const res = await this.http.get<Envelope<LlmModelsResponse>>(
      `/api/llm-settings/models?provider=${encodeURIComponent(provider)}`
    );
    const models = res.data?.models ?? [];
    this.modelsCache.set(provider, { fetchedAt: Date.now(), models });
    return models;
  }

  async getAgentSettings(): Promise<AgentLlmSettingMapDto[]> {
    const res = await this.http.get<Envelope<AgentLlmSettingsDto>>('/api/llm-settings/agents');
    return res.data?.agents ?? [];
  }

  async setAgentSetting(agentId: string, provider: string, model: string): Promise<AgentLlmSettingDto> {
    const body: LlmSettingsRequest = { provider, model };
    const res = await this.http.put<Envelope<AgentLlmSettingDto>>(
      `/api/llm-settings/agents/${encodeURIComponent(agentId)}`,
      body
    );
    return res.data!;
  }
}
