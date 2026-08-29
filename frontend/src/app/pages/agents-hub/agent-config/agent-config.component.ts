import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LlmSettingsService } from '@app/services/llm-settings.service';
import { AgentService } from '@app/services/agent.service';

interface ProviderInfo {
  name: string;
  label: string;
  available: boolean;
}

interface ModelRow {
  provider: string;
  model: string;
  models: string[];
  loadingModels: boolean;
  modelsError: string;
}

type AgentKind = 'llm' | 'deterministic' | 'conversation';

interface AgentRow extends ModelRow {
  agentId: string;
  name: string;
  role: string;
  kind: AgentKind;
  saving: boolean;
  saved: boolean;
  error: string;
  hasOverride: boolean;
}

const DEFAULT_PROVIDER = 'groq';
const REQUEST_TIMEOUT_MS = 12000;

@Component({
  selector: 'app-agent-config',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './agent-config.component.html',
  styleUrl: './agent-config.component.scss'
})
export class AgentConfigComponent implements OnInit {
  private readonly llmSvc = inject(LlmSettingsService);
  private readonly agentSvc = inject(AgentService);

  syncing = true;
  loadError = '';
  providers: ProviderInfo[] = [];
  private modelsCache = new Map<string, string[]>();
  private modelsCacheFetchedAt = new Map<string, number>();
  private static readonly MODELS_TTL_MS = 10 * 60 * 1000;

  general: ModelRow & { saving: boolean; saved: boolean; error: string } = {
    provider: DEFAULT_PROVIDER,
    model: '',
    models: [],
    loadingModels: true,
    modelsError: '',
    saving: false,
    saved: false,
    error: '',
  };
  private generalSnapshot = { provider: '', model: '' };

  agents: AgentRow[] = [];
  private snapshots = new Map<string, { provider: string; model: string }>();

  ngOnInit(): void {
    void this.load();
  }

  retry(): void {
    this.syncing = true;
    this.loadError = '';
    void this.load();
  }

  private availableProviders(all: ProviderInfo[]): string[] {
    return all.filter(p => p.available).map(p => p.name);
  }

  private withTimeout<T>(promise: Promise<T>, fallback: T, ms = REQUEST_TIMEOUT_MS): Promise<T> {
    return new Promise<T>(resolve => {
      const timer = setTimeout(() => resolve(fallback), ms);
      promise.then(
        value => { clearTimeout(timer); resolve(value); },
        () => { clearTimeout(timer); resolve(fallback); },
      );
    });
  }

  private saveWithTimeout(promise: Promise<unknown>, ms = REQUEST_TIMEOUT_MS): Promise<void> {
    return new Promise<void>((resolve, reject) => {
      const timer = setTimeout(() => {
        reject(new Error('Save timed out — please retry'));
      }, ms);
      promise.then(
        () => { clearTimeout(timer); resolve(); },
        err => { clearTimeout(timer); reject(err); },
      );
    });
  }

  private blankAgent(id: string, name: string, role: string, kind: AgentKind, provider: string, model: string, hasOverride: boolean): AgentRow {
    return {
      agentId: id,
      name,
      role,
      kind,
      provider,
      model,
      models: [],
      loadingModels: true,
      modelsError: '',
      saving: false,
      saved: false,
      error: '',
      hasOverride,
    };
  }

  private async load(): Promise<void> {
    try {
      const [providers, catalog, saved] = await Promise.all([
        this.withTimeout(this.llmSvc.getProviders(), []),
        this.withTimeout(this.agentSvc.getAgents(), []),
        this.withTimeout(this.llmSvc.getAgentSettings(), []),
      ]);
      this.providers = providers;
      const available = this.availableProviders(providers);
      const fallback = available.includes(DEFAULT_PROVIDER) ? DEFAULT_PROVIDER : (available[0] ?? 'groq');

      const global = await this.withTimeout(this.llmSvc.getSettings().catch(() => null), null);
      this.general.provider = global?.provider || fallback;
      this.general.model = global?.model ?? '';
      this.generalSnapshot = { provider: this.general.provider, model: this.general.model };

      const savedByAgent = new Map(saved.map(s => [s.agent_id, s]));

      this.agents = catalog.map(a => {
        const row = savedByAgent.get(a.id);
        return this.blankAgent(
          a.id,
          a.name,
          a.role,
          a.id === 'search-agent' ? 'deterministic' : 'llm',
          row?.provider ?? this.general.provider,
          row?.model ?? this.general.model,
          !!(row?.provider && row?.model),
        );
      });

      const bimeSaved = savedByAgent.get('bime');
      this.agents.push(this.blankAgent(
        'bime',
        'BIME',
        'Conversational assistant for CV and job-application tasks',
        'conversation',
        bimeSaved?.provider ?? this.general.provider,
        bimeSaved?.model ?? this.general.model,
        !!(bimeSaved?.provider && bimeSaved?.model),
      ));

      this.agents.forEach(row => {
        if (!this.snapshots.has(row.agentId)) {
          this.snapshots.set(row.agentId, { provider: row.provider, model: row.model });
        }
      });

      if (this.providers.length === 0) {
        this.loadError = 'No LLM providers are available. Check your sidecar and API keys.';
      }

      // Catalog is ready — hide the sync strip; model lists stream in per row.
      this.syncing = false;

      await Promise.all([
        this.loadModelsFor(this.general),
        ...this.agents.map(row => this.loadModelsFor(row)),
      ]);
    } catch (err) {
      console.error('agent-config load failed', err);
      this.loadError = err instanceof Error ? err.message : 'Failed to load configuration';
    } finally {
      this.syncing = false;
    }
  }

  private async fetchModels(provider: string): Promise<{ models: string[]; timedOut: boolean }> {
    const cachedAt = this.modelsCacheFetchedAt.get(provider) ?? 0;
    const cached = this.modelsCache.get(provider);
    if (cached && (Date.now() - cachedAt) < AgentConfigComponent.MODELS_TTL_MS) {
      return { models: cached, timedOut: false };
    }
    this.modelsCache.delete(provider);
    this.modelsCacheFetchedAt.delete(provider);
    return new Promise(resolve => {
      const timer = setTimeout(() => resolve({ models: [], timedOut: true }), REQUEST_TIMEOUT_MS);
      this.llmSvc.getModels(provider).then(
        list => {
          clearTimeout(timer);
          this.modelsCache.set(provider, list);
          this.modelsCacheFetchedAt.set(provider, Date.now());
          resolve({ models: list, timedOut: false });
        },
        () => { clearTimeout(timer); resolve({ models: [], timedOut: false }); },
      );
    });
  }

  private async loadModelsFor(row: ModelRow): Promise<void> {
    row.loadingModels = true;
    row.modelsError = '';
    const provider = row.provider;
    try {
      const { models, timedOut } = await this.fetchModels(provider);
      row.models = models;
      if (timedOut) {
        row.modelsError = 'Timed out fetching models — the provider may be slow or unavailable';
      } else if (models.length === 0) {
        row.modelsError = 'No models returned for this provider';
      } else if (!models.includes(row.model)) {
        row.model = models[0] ?? '';
      }
    } catch {
      row.models = [];
      row.modelsError = 'Could not fetch models for this provider';
    } finally {
      row.loadingModels = false;
    }
  }

  onProviderChange(row: ModelRow, event: Event): void {
    row.provider = (event.target as HTMLSelectElement).value;
    row.model = '';
    void this.loadModelsFor(row);
  }

  hasGeneralChanges(): boolean {
    return this.general.provider !== this.generalSnapshot.provider
      || this.general.model !== this.generalSnapshot.model;
  }

  async saveGeneral(): Promise<void> {
    if (!this.general.provider || !this.general.model) return;
    this.general.saving = true;
    this.general.saved = false;
    this.general.error = '';
    try {
      await this.saveWithTimeout(this.llmSvc.setSettings(this.general.provider, this.general.model));
      this.generalSnapshot = { provider: this.general.provider, model: this.general.model };
      this.general.saved = true;
      setTimeout(() => this.general.saved = false, 2500);
    } catch (err) {
      this.general.error = err instanceof Error ? err.message : 'Failed to save';
    } finally {
      this.general.saving = false;
    }
  }

  hasAgentChanges(row: AgentRow): boolean {
    const saved = this.snapshots.get(row.agentId);
    return saved ? (saved.provider !== row.provider || saved.model !== row.model) : true;
  }

  async saveAgent(row: AgentRow): Promise<void> {
    if (!row.provider || !row.model) return;
    row.saving = true;
    row.saved = false;
    row.error = '';
    try {
      await this.saveWithTimeout(this.llmSvc.setAgentSetting(row.agentId, row.provider, row.model));
      this.snapshots.set(row.agentId, { provider: row.provider, model: row.model });
      row.hasOverride = true;
      row.saved = true;
      setTimeout(() => row.saved = false, 2500);
    } catch (err) {
      row.error = err instanceof Error ? err.message : 'Failed to save';
    } finally {
      row.saving = false;
    }
  }

  resolvedModel(row: AgentRow): { provider: string; model: string; fromOverride: boolean } {
    if (row.hasOverride) {
      return { provider: row.provider, model: row.model, fromOverride: true };
    }
    return { provider: this.general.provider, model: this.general.model, fromOverride: false };
  }
}