import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CompanyService, CompanyDto, CreateCompanyDto } from '@app/services/company.service';
import { ApplicationService } from '@app/services/application.service';
import { ToastService } from '@app/services/toast.service';
import { SheetImportDialogComponent } from '@app/shared/components/sheet-import-dialog/sheet-import-dialog.component';

@Component({
  selector: 'app-companies',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SheetImportDialogComponent],
  templateUrl: './companies.component.html',
  styleUrl: './companies.component.scss',
})
export class CompaniesComponent implements OnInit {
  private readonly companyService = inject(CompanyService);
  private readonly appService = inject(ApplicationService);
  private readonly toast = inject(ToastService);

  companies = signal<CompanyDto[]>([]);
  loading = signal(true);
  search = signal('');
  searchDebounce: ReturnType<typeof setTimeout> | null = null;

  appCounts = signal<Map<string, number>>(new Map());

  dialogOpen = signal(false);
  editingId = signal<string | null>(null);
  saving = signal(false);
  importOpen = signal(false);

  formName = signal('');
  formWebsite = signal('');
  formLocation = signal('');
  formLocationUrl = signal('');
  formNote = signal('');

  filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    if (!term) return this.companies();
    return this.companies().filter(c =>
      c.name.toLowerCase().includes(term) ||
      (c.location ?? '').toLowerCase().includes(term) ||
      (c.note ?? '').toLowerCase().includes(term));
  });

  async ngOnInit() {
    await Promise.all([this.loadCompanies(), this.loadAppCounts()]);
  }

  async loadCompanies() {
    this.loading.set(true);
    try {
      const res = await this.companyService.getCompanies({ pageSize: 500 });
      this.companies.set(res.data?.items ?? []);
    } catch {
      this.toast.error('Failed to load companies');
    } finally {
      this.loading.set(false);
    }
  }

  private async loadAppCounts() {
    try {
      const res = await this.appService.getAll({ page: 1, pageSize: 500 });
      const map = new Map<string, number>();
      for (const app of res.data?.items ?? []) {
        const key = app.companyName.trim().toLowerCase();
        map.set(key, (map.get(key) ?? 0) + 1);
      }
      this.appCounts.set(map);
    } catch { /* non-critical */ }
  }

  onSearchInput(value: string) {
    this.search.set(value);
  }

  appCount(name: string): number {
    return this.appCounts().get(name.trim().toLowerCase()) ?? 0;
  }

  openCreate() {
    this.editingId.set(null);
    this.formName.set('');
    this.formWebsite.set('');
    this.formLocation.set('');
    this.formLocationUrl.set('');
    this.formNote.set('');
    this.dialogOpen.set(true);
  }

  openEdit(c: CompanyDto) {
    this.editingId.set(c.id);
    this.formName.set(c.name);
    this.formWebsite.set(c.websiteUrl ?? '');
    this.formLocation.set(c.location ?? '');
    this.formLocationUrl.set(c.locationUrl ?? '');
    this.formNote.set(c.note ?? '');
    this.dialogOpen.set(true);
  }

  closeDialog() {
    this.dialogOpen.set(false);
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
        locationUrl: this.formLocationUrl().trim() || undefined,
        note: this.formNote().trim() || undefined,
      };
      const id = this.editingId();
      if (id) {
        await this.companyService.updateCompany(id, dto);
        this.toast.success('Company updated');
      } else {
        await this.companyService.createCompany(dto);
        this.toast.success('Company added');
      }
      this.dialogOpen.set(false);
      await this.loadCompanies();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.toast.error(msg || 'Something went wrong');
    } finally {
      this.saving.set(false);
    }
  }

  async onDelete(c: CompanyDto) {
    if (!confirm(`Remove "${c.name}" from your list?`)) return;
    try {
      await this.companyService.deleteCompany(c.id);
      this.toast.success('Company removed');
      await this.loadCompanies();
    } catch {
      this.toast.error('Failed to delete company');
    }
  }

  hostOf(url?: string | null): string {
    if (!url) return '';
    try { return new URL(url).host.replace(/^www\./, ''); } catch { return url; }
  }
}
