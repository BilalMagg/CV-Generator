import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ExtractionService } from '@app/services/extraction.service';
import { ExtractionHistoryItem } from '@app/models/extraction.types';

type InputMethod = 'text' | 'file' | 'url' | 'offer';

@Component({
  selector: 'app-job-extractor-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './job-extractor-workspace.component.html',
  styleUrl: './job-extractor-workspace.component.scss'
})
export class JobExtractorWorkspaceComponent implements OnInit {
  private readonly extractionService = inject(ExtractionService);
  private readonly router = inject(Router);

  activeMethod: InputMethod = 'text';
  textInput = '';
  selectedFile: File | null = null;
  urlInput = '';
  jobOfferId = '';
  language = 'en';

  loading = false;
  error = '';

  history: ExtractionHistoryItem[] = [];
  totalExtractions = 0;
  avgConfidence = 0;

  ngOnInit(): void {
    this.loadHistory();
  }

  private async loadHistory(): Promise<void> {
    try {
      this.history = await this.extractionService.getHistory('job-extractor');
      this.totalExtractions = this.history.length;
      this.avgConfidence = this.history.length
        ? Math.round(this.history.reduce((s, h) => s + h.overallConfidence, 0) / this.history.length * 100)
        : 0;
    } catch {
      this.history = [];
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
  }

  get canSubmit(): boolean {
    switch (this.activeMethod) {
      case 'text': return this.textInput.trim().length > 0;
      case 'file': return this.selectedFile !== null;
      case 'url': return this.urlInput.trim().length > 0;
      case 'offer': return this.jobOfferId.trim().length > 0;
    }
  }

  async onSubmit(): Promise<void> {
    if (!this.canSubmit || this.loading) return;
    this.loading = true;
    this.error = '';

    try {
      const { id, output } = await this.extractionService.extract({
        text: this.activeMethod === 'text' ? this.textInput : undefined,
        file: this.activeMethod === 'file' ? this.selectedFile ?? undefined : undefined,
        url: this.activeMethod === 'url' ? this.urlInput : undefined,
        jobOfferId: this.activeMethod === 'offer' ? this.jobOfferId : undefined,
        language: this.language,
      });
      await this.router.navigate(['/agents-hub/job-extractor/result', id], {
        state: { output }
      });
    } catch (err) {
      this.error = this.extractError(err);
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

  private extractError(err: unknown): string {
    if (err instanceof HttpErrorResponse) {
      if (err.error?.message) return err.error.message;
      if (err.status === 0) return 'Network error — check your connection';
      if (err.status >= 500) return 'Server error — try again later';
      return err.error?.message || err.message || 'Extraction failed';
    }
    return err instanceof Error ? err.message : 'Extraction failed';
  }

  methodIcon(method: string): string {
    switch (method) {
      case 'text': return 'T';
      case 'file': return 'F';
      case 'url': return 'U';
      case 'offer': return 'O';
      default: return '?';
    }
  }
}
