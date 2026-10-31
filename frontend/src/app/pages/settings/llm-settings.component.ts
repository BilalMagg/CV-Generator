import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LlmSettingsService } from '@app/services/llm-settings.service';
import type { LlmProviderInfo, UserLlmKeyDto } from '@app/models/llm-settings.model';

@Component({
  selector: 'app-llm-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './llm-settings.component.html',
  styleUrl: './llm-settings.component.scss',
})
export class LlmSettingsComponent implements OnInit {
  private readonly llmSvc = inject(LlmSettingsService);

  providers = signal<LlmProviderInfo[]>([]);
  models = signal<string[]>([]);
  selectedProvider = signal('');
  selectedModel = signal('');
  loading = signal(true);
  modelsLoading = signal(false);
  saving = signal(false);
  error = signal('');
  showSuccess = signal(false);

  // ── API keys ──────────────────────────────────────────────────────────────
  /** provider → stored key info */
  keys = signal<Record<string, UserLlmKeyDto>>({});
  keyDrafts = signal<Record<string, string>>({});
  keyBusy = signal<Record<string, boolean>>({});
  keyError = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.load();
  }

  private async load(): Promise<void> {
    try {
      const [providers, settings, keys] = await Promise.all([
        this.withTimeout(this.llmSvc.getProviders(), []),
        this.withTimeout(this.llmSvc.getSettings(), null),
        this.withTimeout(this.llmSvc.getKeys(), [] as UserLlmKeyDto[]),
      ]);
      const available = providers.filter(p => p.available);
      this.providers.set(providers);
      this.keys.set(Object.fromEntries((keys ?? []).map(k => [k.provider, k])));

      let initial = settings && settings.provider ? settings : null;
      if (!initial && available.length > 0) initial = { provider: available[0].name, model: '', updated_at: '' };

      if (initial) {
        this.selectedProvider.set(initial.provider);
        await this.loadModelsFor(initial.provider);
        if (settings?.model) this.selectedModel.set(settings.model);
      }
    } catch {
      this.error.set('Failed to load LLM configuration.');
    } finally {
      this.loading.set(false);
    }
  }

  async onProviderChange(provider: string): Promise<void> {
    this.selectedProvider.set(provider);
    this.selectedModel.set('');
    await this.loadModelsFor(provider);
  }

  private withTimeout<T>(promise: Promise<T>, fallback: T, ms = 15000): Promise<T> {
    return new Promise<T>(resolve => {
      const timer = setTimeout(() => resolve(fallback), ms);
      promise.then(
        v => { clearTimeout(timer); resolve(v); },
        () => { clearTimeout(timer); resolve(fallback); },
      );
    });
  }

  private async loadModelsFor(provider: string): Promise<void> {
    if (!provider) return;
    this.modelsLoading.set(true);
    this.models.set([]);
    try {
      const models = await this.withTimeout(this.llmSvc.getModels(provider), []);
      this.models.set(models);
      if (models.length === 0) {
        this.error.set(`No models returned for ${provider}.`);
      } else if (!this.selectedModel()) {
        this.selectedModel.set(models[0]);
      }
    } catch {
      this.error.set(`Failed to load models for ${provider}.`);
    } finally {
      this.modelsLoading.set(false);
    }
  }

  async save(): Promise<void> {
    if (!this.selectedProvider() || !this.selectedModel()) return;
    this.saving.set(true);
    this.error.set('');
    try {
      await this.llmSvc.setSettings(this.selectedProvider(), this.selectedModel());
      this.showSuccess.set(true);
      setTimeout(() => this.showSuccess.set(false), 2400);
    } catch {
      this.error.set('Failed to save LLM settings.');
    } finally {
      this.saving.set(false);
    }
  }

  // ── API keys ──────────────────────────────────────────────────────────────

  keyState(provider: string): UserLlmKeyDto | undefined {
    return this.keys()[provider];
  }

  keyBadge(provider: string): { text: string; cls: string } {
    const stored = this.keyState(provider);
    if (stored) return { text: `Saved ${stored.masked ?? ''}`.trim(), cls: 'saved' };
    const info = this.providers().find(p => p.name === provider);
    if (info?.available) return { text: 'Server env key', cls: 'env' };
    return { text: 'Not configured', cls: 'none' };
  }

  setKeyDraft(provider: string, value: string): void {
    this.keyDrafts.set({ ...this.keyDrafts(), [provider]: value });
  }

  keyDraft(provider: string): string {
    return this.keyDrafts()[provider] ?? '';
  }

  async saveKey(provider: string): Promise<void> {
    const draft = (this.keyDrafts()[provider] ?? '').trim();
    if (!draft) return;
    this.keyBusy.set({ ...this.keyBusy(), [provider]: true });
    try {
      const saved = await this.llmSvc.setKey(provider, draft);
      this.keys.set({ ...this.keys(), [provider]: saved });
      this.keyDrafts.set({ ...this.keyDrafts(), [provider]: '' });
      this.keyError.set({ ...this.keyError(), [provider]: '' });
      // Provider just became selectable in the main picker if it wasn't.
      const info = this.providers().find(p => p.name === provider);
      if (info && !info.available) {
        this.providers.set(this.providers().map(p => p.name === provider ? { ...p, available: true } : p));
      }
      if (this.selectedProvider() === provider) await this.loadModelsFor(provider);
    } catch {
      this.keyError.set({ ...this.keyError(), [provider]: 'Could not save this key. Try again.' });
    } finally {
      this.keyBusy.set({ ...this.keyBusy(), [provider]: false });
    }
  }

  async removeKey(provider: string): Promise<void> {
    this.keyBusy.set({ ...this.keyBusy(), [provider]: true });
    try {
      await this.llmSvc.removeKey(provider);
      const next = { ...this.keys() };
      delete next[provider];
      this.keys.set(next);
      // Selection availability re-derives on next page load (env key would restore it anyway).
      this.providers.set(this.providers().map(p => p.name === provider ? { ...p, available: false, has_key: false } : p));
      if (this.selectedProvider() === provider) await this.loadModelsFor(provider);
    } catch {
      this.keyError.set({ ...this.keyError(), [provider]: 'Could not remove the key.' });
    } finally {
      this.keyBusy.set({ ...this.keyBusy(), [provider]: false });
    }
  }
}
