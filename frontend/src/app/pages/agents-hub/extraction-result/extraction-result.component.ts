import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { ExtractorOutput } from '@app/models/extraction.types';
import { HttpErrorResponse } from '@angular/common/http';
import { Subject, skip, takeUntil } from 'rxjs';

@Component({
  selector: 'app-extraction-result',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './extraction-result.component.html',
  styleUrl: './extraction-result.component.scss'
})
export class ExtractionResultComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly extractionService = inject(ExtractionService);
  private readonly destroy$ = new Subject<void>();

  output: ExtractorOutput | null = null;
  loading = true;
  error = '';
  copied = false;

  editingField: string | null = null;
  editBuffer = '';

  ngOnInit(): void {
    const navigation = this.router.getCurrentNavigation();
    const state = navigation?.extras?.state as { output?: ExtractorOutput } | null;
    const id = this.route.snapshot.paramMap.get('id');

    if (state?.output) {
      this.output = state.output;
      this.loading = false;
      this.cdr.detectChanges();
    } else if (id) {
      this.loadResult(id);
      this.route.paramMap.pipe(
        skip(1),
        takeUntil(this.destroy$),
      ).subscribe(params => {
        const pid = params.get('id');
        if (pid) this.loadResult(pid);
      });
    } else {
      this.error = 'No extraction ID provided';
      this.loading = false;
    }
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

  confidenceColor(score: number): string {
    if (score >= 0.8) return '#16a34a';
    if (score >= 0.5) return '#d97706';
    return '#dc2626';
  }

  // --- Editing ---

  startEdit(field: string): void {
    if (!this.output) return;
    this.editingField = field;
    const val = (this.output as unknown as Record<string, unknown>)[field];
    this.editBuffer = val != null ? String(val) : '';
    setTimeout(() => {
      const el = document.querySelector(`[data-field="${field}"]`) as HTMLInputElement | HTMLTextAreaElement | null;
      el?.focus();
      el?.select();
    });
  }

  saveEdit(field: string): void {
    if (!this.output || this.editingField !== field) return;
    const val = this.editBuffer.trim();
    const target = this.output as unknown as Record<string, unknown>;
    if (val === '') {
      target[field] = undefined;
    } else {
      const original = target[field];
      if (typeof original === 'number') {
        const num = Number(val);
        target[field] = isNaN(num) ? val : num;
      } else {
        target[field] = val;
      }
    }
    this.editingField = null;
  }

  cancelEdit(): void {
    this.editingField = null;
  }

  removeTag(field: string, index: number): void {
    if (!this.output) return;
    const arr = (this.output as unknown as Record<string, unknown>)[field] as unknown[] | undefined;
    if (arr) arr.splice(index, 1);
  }

  addTag(field: string, input: HTMLInputElement): void {
    if (!this.output) return;
    const val = input.value.trim();
    if (!val) return;
    const arr = (this.output as unknown as Record<string, unknown>)[field] as unknown[] | undefined;
    if (arr) arr.push(val);
    input.value = '';
    input.focus();
  }

  // --- Actions ---

  async copyJson(): Promise<void> {
    if (!this.output) return;
    try {
      await navigator.clipboard.writeText(JSON.stringify(this.output, null, 2));
      this.copied = true;
      setTimeout(() => this.copied = false, 2000);
    } catch { /* fallback ignored */ }
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

  isEditing(field: string): boolean {
    return this.editingField === field;
  }

  fieldValue(field: string): string {
    if (!this.output) return '—';
    const val = (this.output as unknown as Record<string, unknown>)[field];
    return val != null ? String(val) : '—';
  }

  tagList(field: string): string[] {
    if (!this.output) return [];
    const val = (this.output as unknown as Record<string, unknown>)[field];
    return Array.isArray(val) ? val as string[] : [];
  }
}
