export interface LlmProviderInfo {
  name: string;
  label: string;
  available: boolean;
  has_key: boolean;
}

export interface LlmProvidersResponse {
  providers: LlmProviderInfo[];
}

export interface LlmModelsResponse {
  provider: string;
  models: string[];
}

export interface LlmSettings {
  provider: string;
  model: string;
  updated_at: string;
}

export interface LlmSettingsRequest {
  provider: string;
  model: string;
}

export interface AgentLlmSettingMapDto {
  agent_id: string;
  name: string;
  provider?: string | null;
  model?: string | null;
}

export interface AgentLlmSettingsDto {
  agents: AgentLlmSettingMapDto[];
}

export interface AgentLlmSettingDto {
  agent_id: string;
  provider: string;
  model: string;
  updated_at: string;
}
