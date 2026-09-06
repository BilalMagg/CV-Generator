import { Component, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AutofillService, AutofillField } from '@app/services/autofill.service';
import { ToastService } from '@app/services/toast.service';

@Component({
  selector: 'app-auto-fill-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './auto-fill-dialog.component.html',
  styleUrl: './auto-fill-dialog.component.scss',
})
export class AutoFillDialogComponent {
  private readonly autofillService = inject(AutofillService);
  private readonly toast = inject(ToastService);

  /** Two-way bound visibility; parent uses [(open)]. */
  open = model(false);
  /** Entity label for the heading, e.g. "company", "project". */
  entityLabel = input('form');
  /** The exact fields the targeted form exposes (name/label/type/options). */
  fields = input<AutofillField[]>([]);
  /** Prepulates the paste box (e.g. an existing description that the user wants to refine). */
  initialText = input('');

  desc = signal('');
  loading = signal(false);
  error = signal('');
  result = signal<Record<string, any> | null>(null);

  /** Emits the extracted field->value map when the user confirms. */
  filled = output<Record<string, any>>();

  openModal(): void {
    this.resetState();
  }

  private resetState(): void {
    this.error.set('');
    this.result.set(null);
    this.loading.set(false);
    this.desc.set(this.initialText() || '');
  }

  async runExtract(): Promise<void> {
    const text = this.desc().trim();
    if (!text) {
      this.toast.error('Paste a description to auto-fill from');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.result.set(null);
    try {
      const res = await this.autofillService.extract({
        entityType: '',
        text,
        fields: this.fields(),
      });
      const values = res?.data?.values;
      if (!values) {
        throw new Error('No values returned');
      }
      const populated: Record<string, any> = {};
      for (const f of this.fields()) {
        if (values[f.name] !== undefined && values[f.name] !== null && values[f.name] !== '') {
          populated[f.name] = values[f.name];
        }
      }
      if (Object.keys(populated).length === 0) {
        this.toast.error('Could not extract any fields. Try giving a more detailed description.');
        this.result.set(null);
        return;
      }
      // Auto-apply: emit the filled values straight into the form and close.
      this.filled.emit(populated);
      this.result.set(populated);
      this.open.set(false);
      this.toast.success(`Auto-filled ${Object.keys(populated).length} field(s) — review before saving`);
    } catch {
      this.toast.error('Auto-fill failed. Please try again in a moment.');
      this.error.set('The auto-fill service could not extract the details. Check that the AI agents are running, then try again.');
    } finally {
      this.loading.set(false);
    }
  }

  close(): void {
    this.open.set(false);
  }
}
