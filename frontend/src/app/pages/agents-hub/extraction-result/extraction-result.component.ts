import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { ExtractorOutput } from '@app/models/extraction.types';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-extraction-result',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './extraction-result.component.html',
  styleUrl: './extraction-result.component.scss'
})
export class ExtractionResultComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly extractionService = inject(ExtractionService);
  private readonly destroy$ = new Subject<void>();

  output: ExtractorOutput | null = null;
  loading = true;
  error = '';
  copied = false;

  ngOnInit(): void {
    this.route.paramMap.pipe(
      takeUntil(this.destroy$),
    ).subscribe(params => {
      const id = params.get('id');
      if (id) this.loadResult(id);
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private async loadResult(id: string): Promise<void> {
    try {
      this.output = await this.extractionService.getExtraction(id);
    } catch (err) {
      this.error = this.extractError(err);
    } finally {
      this.loading = false;
    }
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

  get confidenceLabel(): string {
    if (!this.output) return '';
    const pct = this.output.overallConfidence * 100;
    return `${Math.round(pct)}%`;
  }

  get confidenceLevel(): 'high' | 'mid' | 'low' {
    if (!this.output) return 'low';
    if (this.output.overallConfidence >= 0.8) return 'high';
    if (this.output.overallConfidence >= 0.5) return 'mid';
    return 'low';
  }

  fieldConfidence(field: string): number {
    return this.output?.fieldConfidences?.[field] ?? 0;
  }

  fieldConfidenceLevel(score: number): string {
    if (score >= 0.8) return 'high';
    if (score >= 0.5) return 'mid';
    return 'low';
  }

  confidenceColor(score: number): string {
    if (score >= 0.8) return '#16a34a';
    if (score >= 0.5) return '#d97706';
    return '#dc2626';
  }

  async copyJson(): Promise<void> {
    if (!this.output) return;
    try {
      await navigator.clipboard.writeText(JSON.stringify(this.output, null, 2));
      this.copied = true;
      setTimeout(() => this.copied = false, 2000);
    } catch {
      // Fallback
    }
  }

  exportJson(): void {
    if (!this.output) return;
    const blob = new Blob([JSON.stringify(this.output, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'extraction-result.json';
    a.click();
    URL.revokeObjectURL(url);
  }

  hasValue(val: unknown): boolean {
    if (val === null || val === undefined) return false;
    if (typeof val === 'string') return val.trim().length > 0;
    if (Array.isArray(val)) return val.length > 0;
    return true;
  }
}
