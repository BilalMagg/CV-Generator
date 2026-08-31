import { Component, computed, inject, input, model, OnChanges, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyService, CompanyDto, CreateCompanyDto } from '@app/services/company.service';
import { ToastService } from '@app/services/toast.service';
import { COUNTRIES, MOROCCO_CITIES } from '@app/shared/data/geo-data';
import { AutoFillDialogComponent } from '@app/shared/components/auto-fill-dialog/auto-fill-dialog.component';
import { AutofillField } from '@app/services/autofill.service';

@Component({
  selector: 'app-company-form-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, AutoFillDialogComponent],
  templateUrl: './company-form-dialog.component.html',
  styleUrl: './company-form-dialog.component.scss',
})
export class CompanyFormDialogComponent implements OnChanges {
  private readonly companyService = inject(CompanyService);
  private readonly toast = inject(ToastService);

  /** Two-way bound visibility; parent uses [(open)]. */
  open = model(false);
  /** Company being edited, or null/undefined for create mode. */
  editing = input<CompanyDto | null>(null);
  /** Optional company name prefill for create mode (e.g. coming from a detail page CTA). */
  prefillName = input('');
  /** Emits the created/updated company so parents can refresh or update in place. */
  saved = output<CompanyDto>();

  saving = signal(false);
  formName = signal('');
  formWebsite = signal('');
  formLocation = signal('');
  formCountry = signal('Morocco');
  formLocationUrl = signal('');
  formNote = signal('');
  formDescription = signal('');

  protected readonly COUNTRIES = COUNTRIES;

  citySuggestions = computed(() =>
    this.formCountry() === 'Morocco' ? MOROCCO_CITIES : []);

  autofillOpen = signal(false);

  autofillFields: AutofillField[] = [
    { name: 'formName', label: 'Company name', type: 'text' },
    { name: 'formWebsite', label: 'Website URL', type: 'url' },
    { name: 'formLocation', label: 'City', type: 'text' },
    { name: 'formCountry', label: 'Country', type: 'text' },
    { name: 'formLocationUrl', label: 'Location URL', type: 'url' },
    { name: 'formNote', label: 'Note', type: 'textarea' },
    { name: 'formDescription', label: 'Description', type: 'textarea' },
  ];

  openAutofill(): void {
    this.autofillOpen.set(true);
  }

  applyAutofill(values: Record<string, any>): void {
    if (values['formName'] !== undefined) this.formName.set(String(values['formName']));
    if (values['formWebsite'] !== undefined) this.formWebsite.set(String(values['formWebsite']));
    if (values['formLocation'] !== undefined) this.formLocation.set(String(values['formLocation']));
    if (values['formCountry'] !== undefined) {
      const country = String(values['formCountry']);
      const countries = new Set((COUNTRIES as string[]).map(c => c.toLowerCase()));
      this.formCountry.set(countries.has(country.toLowerCase()) ? country : 'Morocco');
    }
    if (values['formLocationUrl'] !== undefined) this.formLocationUrl.set(String(values['formLocationUrl']));
    if (values['formNote'] !== undefined) this.formNote.set(String(values['formNote']));
    if (values['formDescription'] !== undefined) this.formDescription.set(String(values['formDescription']));
  }

  ngOnChanges(): void {
    if (!this.open()) return;
    const c = this.editing();
    if (c) {
      this.formName.set(c.name);
      this.formWebsite.set(c.websiteUrl ?? '');
      this.formLocation.set(c.location ?? '');
      this.formCountry.set(c.country || 'Morocco');
      this.formLocationUrl.set(c.locationUrl ?? '');
      this.formNote.set(c.note ?? '');
      this.formDescription.set(c.description ?? '');
    } else {
      this.formName.set(this.prefillName());
      this.formWebsite.set('');
      this.formLocation.set('');
      this.formCountry.set('Morocco');
      this.formLocationUrl.set('');
      this.formNote.set('');
      this.formDescription.set('');
    }
  }

  close() {
    this.open.set(false);
  }

  async save() {
    const name = this.formName().trim();
    if (!name) {
      this.toast.error('Company name is required');
      return;
    }
    this.saving.set(true);
    try {
      const dto: CreateCompanyDto = {
        name,
        websiteUrl: this.formWebsite().trim() || undefined,
        location: this.formLocation().trim() || undefined,
        country: this.formCountry(),
        locationUrl: this.formLocationUrl().trim() || undefined,
        note: this.formNote().trim() || undefined,
        description: this.formDescription().trim() || undefined,
      };
      const editing = this.editing();
      const result = editing
        ? await this.companyService.updateCompany(editing.id, dto)
        : await this.companyService.createCompany(dto);
      const saved = result.data;
      if (!saved) throw new Error('empty response');
      this.toast.success(editing ? 'Company updated' : 'Company added');
      this.open.set(false);
      this.saved.emit(saved);
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.toast.error(msg || 'Something went wrong');
    } finally {
      this.saving.set(false);
    }
  }
}
