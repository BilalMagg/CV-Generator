import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { ExtractionHistoryItem } from '@app/models/extraction.types';
import { extractError } from '@app/shared/error-utils';

type SearchInputMethod = 'extracted-job' | 'text' | 'keywords';

@Component({
  selector: 'app-search-agent-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './search-agent-workspace.component.html',
  styleUrl: './search-agent-workspace.component.scss'
})
export class SearchAgentWorkspaceComponent implements OnInit {
  // Using ExtractionService temporarily as a placeholder for a future SearchAgentService
  private readonly extractionService = inject(ExtractionService);
  private readonly router = inject(Router);

  activeMethod: SearchInputMethod = 'extracted-job';
  extractedJobId = '';
  textInput = '';
  keywordsInput = '';
  language = 'en';

  loading = false;
  error = '';

  history: ExtractionHistoryItem[] = [];
  totalSearches = 0;
  avgConfidence = 0;

  ngOnInit(): void {
    this.loadHistory();
  }

  private async loadHistory(): Promise<void> {
    try {
      // Mocking history load using extraction service temporarily
      this.history = await this.extractionService.getHistory('search-agent');
      this.totalSearches = this.history.length;
      this.avgConfidence = this.history.length
        ? Math.round(this.history.reduce((s, h) => s + h.overallConfidence, 0) / this.history.length * 100)
        : 0;
    } catch {
      this.history = [];
    }
  }

  get canSubmit(): boolean {
    switch (this.activeMethod) {
      case 'extracted-job': return this.extractedJobId.trim().length > 0;
      case 'text': return this.textInput.trim().length > 0;
      case 'keywords': return this.keywordsInput.trim().length > 0;
    }
  }

  async onSubmit(): Promise<void> {
    if (!this.canSubmit || this.loading) return;
    this.loading = true;
    this.error = '';

    try {
      // In a real app, this would call searchAgentService.search(...)
      // For now we simulate an API delay then just clear loading state
      await new Promise(resolve => setTimeout(resolve, 1500));
      // await this.router.navigate(['/agents-hub/search-agent/result', 'new-id']);
      this.error = 'Search Agent backend is not implemented yet.';
    } catch (err) {
      this.error = extractError(err, 'Search failed');
    } finally {
      this.loading = false;
    }
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - d.getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 60) return `${mins}m ago`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    return d.toLocaleDateString();
  }

  confidenceColor(score: number): string {
    if (score >= 0.8) return 'high';
    if (score >= 0.5) return 'mid';
    return 'low';
  }

  methodIcon(method: string): string {
    switch (method) {
      case 'extracted-job': return 'J';
      case 'text': return 'T';
      case 'keywords': return 'K';
      default: return '?';
    }
  }
}
