import { Component, signal, computed, inject, OnInit, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CompanyService, CompanyDto } from '@app/services/company.service';
import { ToastService } from '@app/services/toast.service';
import { ConfirmService } from '@app/services/confirm.service';
import { ShortcutService } from '@app/services/shortcut.service';
import { SheetImportDialogComponent } from '@app/shared/components/sheet-import-dialog/sheet-import-dialog.component';
import { CompanyFormDialogComponent } from '@app/shared/components/company-form-dialog/company-form-dialog.component';
import { CompanyMergeDialogComponent } from '@app/shared/components/company-merge-dialog/company-merge-dialog.component';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';
import { COUNTRIES } from '@app/shared/data/geo-data';

@Component({
  selector: 'app-companies',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, SheetImportDialogComponent, CompanyFormDialogComponent, CompanyMergeDialogComponent, RefreshButtonComponent],
  templateUrl: './companies.component.html',
  styleUrl: './companies.component.scss',
})
export class CompaniesComponent implements OnInit {
  private readonly companyService = inject(CompanyService);
  private readonly toast = inject(ToastService);
  private readonly confirm = inject(ConfirmService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly shortcut = inject(ShortcutService);
  private readonly destroyRef = inject(DestroyRef);

  companies = signal<CompanyDto[]>([]);
  loading = signal(true);
  search = signal('');
  countryFilter = signal('');
  cityFilter = signal('');
  minAppsFilter = signal<number | null>(null);
  sortBy = signal<'name' | 'apps' | 'applied'>('name');
  sortDir = signal<'asc' | 'desc'>('asc');
  page = signal(1);
  total = signal(0);
  private readonly pageSize = 20;
  protected readonly pageCount = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;
  private cityDebounce: ReturnType<typeof setTimeout> | null = null;

  hasActiveFilters = computed(() =>
    !!this.search().trim() || !!this.countryFilter() || !!this.cityFilter().trim() ||
    (this.minAppsFilter() !== null && this.minAppsFilter() !== 0));

  dialogOpen = signal(false);
  editingCompany = signal<CompanyDto | null>(null);
  importOpen = signal(false);
  refreshing = signal(false);
  mergeOpen = signal(false);
  mergeInitialCompanyId = signal<string | null>(null);

  protected readonly COUNTRIES = COUNTRIES;

  async ngOnInit() {
    this.shortcut.register({ key: 'a', label: 'Add Company', handler: () => this.openCreate() }, this.destroyRef);
    this.shortcut.register({ key: 'c', label: 'Add Company', handler: () => this.openCreate() }, this.destroyRef);
    this.shortcut.register({ key: 'm', label: 'Merge duplicate companies', handler: () => this.openMerge() }, this.destroyRef);

    this.restoreFromUrl();
    await this.loadCompanies();
  }

  /** List state lives in the URL query string so navigating into a company detail and
   * pressing Back (or a fresh link/refresh) restores the exact page, search and filters. */
  private restoreFromUrl(): void {
    const q = this.route.snapshot.queryParamMap;
    if (q.get('create') === '1' || q.get('new') === '1') {
      this.openCreate();
    }
    const page = Number(q.get('page'));
    if (Number.isFinite(page) && page > 1) this.page.set(Math.trunc(page));
    const search = q.get('q');
    if (search) this.search.set(search);
    const country = q.get('country');
    if (country) this.countryFilter.set(country);
    const city = q.get('city');
    if (city) this.cityFilter.set(city);
    const minApps = Number(q.get('minApps'));
    if (Number.isFinite(minApps) && minApps > 0) this.minAppsFilter.set(Math.trunc(minApps));
    const sortBy = q.get('sortBy');
    if (sortBy === 'name' || sortBy === 'apps' || sortBy === 'applied') this.sortBy.set(sortBy);
    if (q.get('dir') === 'desc') this.sortDir.set('desc');
  }

  private buildQueryParams(): Record<string, string | number> {
    const params: Record<string, string | number> = {};
    params['page'] = this.page();
    const search = this.search().trim();
    if (search) params['q'] = search;
    const country = this.countryFilter();
    if (country) params['country'] = country;
    const city = this.cityFilter().trim();
    if (city) params['city'] = city;
    const minApps = this.minAppsFilter();
    if (minApps !== null && minApps > 0) params['minApps'] = minApps;
    const sortBy = this.sortBy();
    if (sortBy !== 'name') params['sortBy'] = sortBy;
    const sortDir = this.sortDir();
    if (sortDir !== 'asc') params['dir'] = sortDir;
    return params;
  }

  /** Mirrors the current list state into the URL (history entry replaced, no reload). */
  private syncUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: this.buildQueryParams(),
      replaceUrl: true,
    });
  }

  async loadCompanies() {
    this.loading.set(true);
    try {
      const res = await this.companyService.getCompanies({
        search: this.search().trim() || undefined,
        country: this.countryFilter() || undefined,
        city: this.cityFilter().trim() || undefined,
        minApps: this.minAppsFilter() ?? undefined,
        sortBy: this.sortBy(),
        sortDir: this.sortDir(),
        page: this.page(),
        pageSize: this.pageSize,
      });
      this.total.set(res.data?.total ?? 0);

      // Clamp the page if it fell off the end (e.g. after deleting the last row).
      const maxPage = Math.max(1, Math.ceil(this.total() / this.pageSize));
      if (this.page() > maxPage) {
        this.page.set(maxPage);
        this.syncUrl();
        this.loading.set(false);
        await this.loadCompanies();
        return;
      }

      this.companies.set(res.data?.items ?? []);
    } catch {
      this.toast.error('Failed to load companies');
    } finally {
      this.loading.set(false);
      this.refreshing.set(false);
    }
  }

  onRefresh() {
    this.refreshing.set(true);
    this.loadCompanies();
  }

  onSearchInput(value: string) {
    this.search.set(value);
    if (this.searchDebounce) clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => {
      this.page.set(1);
      this.syncUrl();
      void this.loadCompanies();
    }, 300);
  }

  onCityInput(value: string) {
    this.cityFilter.set(value);
    if (this.cityDebounce) clearTimeout(this.cityDebounce);
    this.cityDebounce = setTimeout(() => {
      this.page.set(1);
      this.syncUrl();
      void this.loadCompanies();
    }, 300);
  }

  onCountryFilterChange(value: string) {
    this.countryFilter.set(value);
    this.page.set(1);
    this.syncUrl();
    void this.loadCompanies();
  }

  onMinAppsChange(value: string) {
    const parsed = parseInt(value, 10);
    const next = Number.isFinite(parsed) && parsed > 0 ? parsed : null;
    if (next === this.minAppsFilter()) return;
    this.minAppsFilter.set(next);
    this.page.set(1);
    this.syncUrl();
    void this.loadCompanies();
  }

  onSortByChange(value: 'name' | 'apps' | 'applied') {
    this.sortBy.set(value);
    // Sensible default direction per category; still toggleable afterwards.
    this.sortDir.set(value === 'name' ? 'asc' : 'desc');
    this.page.set(1);
    this.syncUrl();
    void this.loadCompanies();
  }

  toggleSortDir() {
    this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    this.syncUrl();
    void this.loadCompanies();
  }

  resetFilters() {
    this.search.set('');
    this.countryFilter.set('');
    this.cityFilter.set('');
    this.minAppsFilter.set(null);
    this.sortBy.set('name');
    this.sortDir.set('asc');
    this.page.set(1);
    this.syncUrl();
    void this.loadCompanies();
  }

  goToPage(target: number) {
    const clamped = Math.min(Math.max(1, target), this.pageCount());
    if (clamped === this.page()) return;
    this.page.set(clamped);
    this.syncUrl();
    void this.loadCompanies();
  }

  /** Windowed page numbers with -1 as an ellipsis marker. */
  visiblePages(): number[] {
    const count = this.pageCount();
    const current = this.page();
    if (count <= 7) return Array.from({ length: count }, (_, i) => i + 1);
    const pages = new Set<number>([1, count, current - 1, current, current + 1]);
    if (current <= 3) [2, 3, 4].forEach(p => pages.add(p));
    if (current >= count - 2) [count - 3, count - 2, count - 1].forEach(p => pages.add(p));
    const sorted = [...pages].filter(p => p >= 1 && p <= count).sort((a, b) => a - b);
    const out: number[] = [];
    let prev = 0;
    for (const p of sorted) {
      if (p - prev > 1) out.push(-1);
      out.push(p);
      prev = p;
    }
    return out;
  }

  openCreate() {
    this.editingCompany.set(null);
    this.dialogOpen.set(true);
  }

  openMerge(initialId?: string) {
    this.mergeInitialCompanyId.set(initialId || null);
    this.mergeOpen.set(true);
  }

  onMerged(_company: CompanyDto) {
    void this.loadCompanies();
  }

  openDetail(c: CompanyDto) {
    void this.router.navigate(['/companies', c.id]);
  }

  openEdit(c: CompanyDto) {
    this.editingCompany.set(c);
    this.dialogOpen.set(true);
  }

  async onDelete(c: CompanyDto) {
    if (!(await this.confirm.confirm({ message: `Remove "${c.name}" from your list?`, variant: 'danger' }))) return;
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

  displayLocation(c: CompanyDto): string {
    if (c.location && c.country) return `${c.location}, ${c.country}`;
    return c.location || c.country || '—';
  }

  formatDate(iso?: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  }
}
