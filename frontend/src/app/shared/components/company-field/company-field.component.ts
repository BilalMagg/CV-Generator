import { Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CompanyService, CompanyDto } from '@app/services/company.service';

export interface CompanyMatchLike {
  name: string;
}

/** Classify the current value against the fetched matches + the last picked company. */
export function companyStatus<T extends CompanyMatchLike>(
  value: string,
  matches: T[],
  pickedName: string | null,
): { exact: T | null; isNew: boolean } {
  const cur = value.trim().toLowerCase();
  if (!cur) return { exact: null, isNew: false };
  const exact = matches.find(m => m.name && m.name.trim().toLowerCase() === cur) ?? null;
  const picked = pickedName?.trim().toLowerCase();
  return { exact, isNew: exact === null && picked !== cur };
}

@Component({
  selector: 'app-company-field',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './company-field.component.html',
  styleUrl: './company-field.component.scss',
})
export class CompanyFieldComponent {
  private companies = inject(CompanyService);

  /** Two-way bound company name — always a free-text value that may or may not exist yet. */
  model = model<string>('');

  placeholder = input<string>('Search or type a company…');

  /** Emitted when the user picks a saved company from the suggestions. */
  picked = output<CompanyDto>();

  matches = signal<CompanyDto[]>([]);
  searching = signal(false);
  open = signal(false);
  pickedCompany = signal<CompanyDto | null>(null);

  private timer: ReturnType<typeof setTimeout> | null = null;
  /** Last term we searched for, so externally-written values get one background lookup. */
  private searchedFor = '';

  /** The saved company whose name exactly equals the current typed value. */
  status = computed(() =>
    companyStatus(this.model(), this.matches(), this.pickedCompany()?.name ?? null),
  );

  constructor() {
    effect(() => {
      const value = this.model();
      const picked = this.pickedCompany();
      if (picked && picked.name.trim().toLowerCase() !== value.trim().toLowerCase()) {
        this.pickedCompany.set(null);
      }
      const term = value.trim();
      if (term && term.toLowerCase() !== this.searchedFor.toLowerCase()) {
        this.searchedFor = term;
        void this.search(term, false);
      }
    });
  }

  onInput(value: string) {
    this.model.set(value);
    const trimmed = value.trim();
    this.searchedFor = trimmed.toLowerCase();
    if (this.pickedCompany() && this.pickedCompany()!.name.trim().toLowerCase() !== trimmed.toLowerCase()) {
      this.pickedCompany.set(null);
    }
    if (!trimmed) {
      if (this.timer) { clearTimeout(this.timer); this.timer = null; }
      this.matches.set([]);
      this.searching.set(false);
      this.open.set(false);
      return;
    }
    this.open.set(true);
    if (this.timer) clearTimeout(this.timer);
    this.timer = setTimeout(() => void this.search(trimmed, true), 220);
  }

  onFocus() {
    if (this.model().trim()) {
      this.searching.set(true);
      this.open.set(true);
      void this.search(this.model().trim(), true);
    }
  }

  onBlur() {
    this.open.set(false);
    this.searching.set(false);
  }

  private async search(term: string, openResult: boolean) {
    this.searching.set(true);
    try {
      const res = await this.companies.getCompanies({ search: term, pageSize: 8, sortBy: 'name' });
      const items = res.success && res.data ? res.data.items : [];
      this.matches.set(items);
      if (openResult) this.open.set(items.length > 0 || this.searching());
    } catch {
      this.matches.set([]);
    } finally {
      this.searching.set(false);
    }
  }

  pick(c: CompanyDto) {
    if (this.timer) { clearTimeout(this.timer); this.timer = null; }
    this.model.set(c.name);
    this.searchedFor = c.name.trim().toLowerCase();
    this.pickedCompany.set(c);
    this.matches.set([]);
    this.searching.set(false);
    this.open.set(false);
    this.picked.emit(c);
  }
}