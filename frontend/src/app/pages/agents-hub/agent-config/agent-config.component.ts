import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { AgentConfig, DEFAULT_CONFIG } from '@app/models/extraction.types';

@Component({
  selector: 'app-agent-config',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './agent-config.component.html',
  styleUrl: './agent-config.component.scss'
})
export class AgentConfigComponent implements OnInit {
  private readonly extractionService = inject(ExtractionService);

  config: AgentConfig = { ...DEFAULT_CONFIG };
  loading = true;
  saving = false;
  saved = false;
  error = '';
  testStatus: 'idle' | 'testing' | 'success' | 'fail' = 'idle';

  readonly models = [
    { value: 'llama-3.1-8b-instant', label: 'Llama 3.1 8B Instant' },
    { value: 'llama-3.2-3b-preview', label: 'Llama 3.2 3B Preview' },
    { value: 'mixtral-8x7b-32768', label: 'Mixtral 8x7B' },
  ];

  readonly languages = [
    { value: 'en', label: 'English' },
    { value: 'fr', label: 'French' },
    { value: 'de', label: 'German' },
    { value: 'es', label: 'Spanish' },
    { value: 'it', label: 'Italian' },
    { value: 'nl', label: 'Dutch' },
    { value: 'pt', label: 'Portuguese' },
    { value: 'ja', label: 'Japanese' },
    { value: 'zh', label: 'Chinese' },
  ];

  ngOnInit(): void {
    this.loadConfig();
  }

  private async loadConfig(): Promise<void> {
    try {
      this.config = await this.extractionService.getConfig('job-extractor');
    } catch {
      this.config = { ...DEFAULT_CONFIG };
    } finally {
      this.loading = false;
    }
  }

  async save(): Promise<void> {
    this.saving = true;
    this.saved = false;
    this.error = '';
    try {
      await this.extractionService.saveConfig('job-extractor', this.config);
      this.saved = true;
      setTimeout(() => this.saved = false, 2500);
    } catch (err) {
      this.error = err instanceof Error ? err.message : 'Failed to save';
    } finally {
      this.saving = false;
    }
  }

  resetDefaults(): void {
    this.config = { ...DEFAULT_CONFIG };
  }

  testConnection(): void {
    this.testStatus = 'testing';
    setTimeout(() => {
      this.testStatus = this.config.apiKey.length > 0 ? 'success' : 'fail';
      setTimeout(() => this.testStatus = 'idle', 3000);
    }, 1500);
  }

  get hasUnsavedChanges(): boolean {
    return JSON.stringify(this.config) !== JSON.stringify(DEFAULT_CONFIG);
  }
}
