import { ChangeDetectorRef, Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ExtractionService } from '@app/services/extraction.service';
import { ExtractorOutput } from '@app/models/extraction.types';
import { Subject, skip, takeUntil } from 'rxjs';
import { extractError } from '@app/shared/error-utils';

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
    const navState = navigation?.extras?.state as { output?: ExtractorOutput } | undefined;
    const histState = history.state as { output?: ExtractorOutput } | undefined;
    const output = navState?.output ?? histState?.output;
    const id = this.route.snapshot.paramMap.get('id');

    if (output) {
      this.output = output;
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
      this.error = extractError(err, 'Extraction failed');
    } finally {
      this.loading = false;
      this.cdr.detectChanges();
    }
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

  fieldConfidencePct(field: string): string {
    return Math.round(this.fieldConfidence(field) * 100) + '%';
  }

  confidenceColor(score: number): string {
    if (score >= 0.8) return '#16a34a';
    if (score >= 0.5) return '#d97706';
    return '#dc2626';
  }

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
