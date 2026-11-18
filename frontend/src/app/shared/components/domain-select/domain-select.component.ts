import {
  Component,
  Input,
  Output,
  EventEmitter,
  forwardRef,
  inject,
  signal,
  computed,
  OnInit,
  ChangeDetectorRef,
  booleanAttribute
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { ApplicationService } from '@app/services/application.service';
import { ApplicationDomainDto } from '@app/models/application.model';
import { UiSelectComponent, SelectOption } from '../ui/select';

@Component({
  selector: 'app-domain-select',
  standalone: true,
  imports: [CommonModule, FormsModule, UiSelectComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DomainSelectComponent),
      multi: true
    }
  ],
  templateUrl: './domain-select.component.html',
  styleUrl: './domain-select.component.scss'
})
export class DomainSelectComponent implements ControlValueAccessor, OnInit {
  private appService = inject(ApplicationService);
  private cdr = inject(ChangeDetectorRef);

  @Input() id = 'domainSelect';
  @Input() placeholder = 'Select domain…';
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input({ transform: booleanAttribute }) isInvalid = false;
  @Input() className = '';

  @Output() valueChange = new EventEmitter<string | undefined>();

  // State
  domains = signal<ApplicationDomainDto[]>([]);
  currentValue = signal<string>('');
  loading = signal(false);

  // Modals state
  showAddModal = signal(false);
  showManageModal = signal(false);

  // Add category form state
  newCategoryName = signal('');
  addError = signal('');
  savingCategory = signal(false);

  // Manage categories state
  editingId = signal<string | null>(null);
  editingName = signal('');
  managingError = signal('');
  confirmDeleteId = signal<string | null>(null);

  // Computed options for <ui-select>
  options = computed<SelectOption[]>(() => {
    const list: SelectOption[] = [
      {
        value: '__action_add',
        label: 'Add category…',
        icon: 'ti ti-plus',
        action: () => this.openAddModal()
      },
      {
        value: '__action_edit',
        label: 'Edit categories…',
        icon: 'ti ti-edit',
        action: () => this.openManageModal()
      },
      {
        value: '__divider_actions',
        label: '',
        isDivider: true
      },
      {
        value: '',
        label: 'Undefined (None)'
      }
    ];

    for (const d of this.domains()) {
      list.push({
        value: d.name,
        label: d.name,
        description: d.applicationCount && d.applicationCount > 0 ? `${d.applicationCount} apps` : undefined
      });
    }

    return list;
  });

  // Exact match detection for add category
  exactMatch = computed(() => {
    const query = this.newCategoryName().trim().toLowerCase();
    if (!query) return null;
    return this.domains().find(d => d.name.trim().toLowerCase() === query) || null;
  });

  // Similarity detection for add category
  similarCategories = computed<string[]>(() => {
    const raw = this.newCategoryName().trim();
    if (!raw || raw.length < 2) return [];

    const existingNames = this.domains().map(d => d.name);
    return this.findSimilar(raw, existingNames);
  });

  private onChange: (val: any) => void = () => {};
  private onTouched: () => void = () => {};

  async ngOnInit() {
    await this.loadDomains();
  }

  async loadDomains() {
    this.loading.set(true);
    try {
      const res = await this.appService.getDomains();
      if (res.success && res.data) {
        this.domains.set(res.data);
      }
    } catch (e) {
      console.error('Failed to load application domains', e);
    } finally {
      this.loading.set(false);
      this.cdr.markForCheck();
    }
  }

  // Value Accessor
  writeValue(value: any): void {
    this.currentValue.set(value || '');
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.disabled = isDisabled;
  }

  onSelectValueChange(val: string) {
    const finalVal = val ? val.trim() : '';
    this.currentValue.set(finalVal);
    this.onChange(finalVal || undefined);
    this.valueChange.emit(finalVal || undefined);
  }

  // Modal Triggers
  openAddModal() {
    this.newCategoryName.set('');
    this.addError.set('');
    this.showAddModal.set(true);
  }

  closeAddModal() {
    this.showAddModal.set(false);
    this.newCategoryName.set('');
    this.addError.set('');
  }

  openManageModal() {
    this.managingError.set('');
    this.editingId.set(null);
    this.confirmDeleteId.set(null);
    this.showManageModal.set(true);
  }

  closeManageModal() {
    this.showManageModal.set(false);
    this.editingId.set(null);
    this.confirmDeleteId.set(null);
  }

  // Create Category
  async submitNewCategory() {
    const name = this.newCategoryName().trim();
    if (!name) return;
    if (this.exactMatch()) {
      this.addError.set(`A category named "${name}" already exists.`);
      return;
    }

    this.savingCategory.set(true);
    this.addError.set('');

    try {
      const res = await this.appService.createDomain(name);
      if (res.success && res.data) {
        // Refresh domains list
        await this.loadDomains();

        // Select the newly created domain
        this.onSelectValueChange(res.data.name);

        this.closeAddModal();
      } else {
        this.addError.set(res.message || 'Failed to create category');
      }
    } catch (err: any) {
      this.addError.set(err?.error?.message || err?.message || 'Error creating category');
    } finally {
      this.savingCategory.set(false);
      this.cdr.markForCheck();
    }
  }

  selectExistingAndClose(name: string) {
    this.onSelectValueChange(name);
    this.closeAddModal();
  }

  // Edit category in manage modal
  startEdit(d: ApplicationDomainDto) {
    this.editingId.set(d.id);
    this.editingName.set(d.name);
    this.confirmDeleteId.set(null);
  }

  cancelEdit() {
    this.editingId.set(null);
    this.editingName.set('');
  }

  async saveEdit(d: ApplicationDomainDto) {
    const newName = this.editingName().trim();
    if (!newName) return;
    if (newName.toLowerCase() === d.name.toLowerCase()) {
      this.cancelEdit();
      return;
    }

    try {
      const res = await this.appService.updateDomain(d.id, newName);
      if (res.success && res.data) {
        const wasSelected = this.currentValue() === d.name;
        await this.loadDomains();
        if (wasSelected) {
          this.onSelectValueChange(res.data.name);
        }
        this.cancelEdit();
      } else {
        this.managingError.set(res.message || 'Failed to update category');
      }
    } catch (err: any) {
      this.managingError.set(err?.error?.message || err?.message || 'Error updating category');
    }
  }

  // Delete category in manage modal
  promptDelete(d: ApplicationDomainDto) {
    this.confirmDeleteId.set(d.id);
    this.editingId.set(null);
  }

  cancelDelete() {
    this.confirmDeleteId.set(null);
  }

  async confirmDelete(d: ApplicationDomainDto) {
    try {
      const res = await this.appService.deleteDomain(d.id);
      if (res.success) {
        const wasSelected = this.currentValue() === d.name;
        await this.loadDomains();
        if (wasSelected) {
          this.onSelectValueChange('');
        }
        this.confirmDeleteId.set(null);
      } else {
        this.managingError.set(res.message || 'Failed to delete category');
      }
    } catch (err: any) {
      this.managingError.set(err?.error?.message || err?.message || 'Error deleting category');
    }
  }

  // Similarity Algorithm
  private findSimilar(query: string, existing: string[]): string[] {
    const results: string[] = [];
    const queryTokens = this.tokenize(query);
    const queryNorm = query.toLowerCase();

    for (const name of existing) {
      if (name.trim().toLowerCase() === queryNorm) continue;

      const nameNorm = name.toLowerCase();
      const nameTokens = this.tokenize(name);

      // Substring match
      if (queryNorm.length >= 3 && nameNorm.includes(queryNorm)) {
        results.push(name);
        continue;
      }
      if (nameNorm.length >= 3 && queryNorm.includes(nameNorm)) {
        results.push(name);
        continue;
      }

      // Token overlap (e.g. "Data & AI" shares "data" with "Data Science", and "ai" with "AI & LLMs")
      const hasOverlap = queryTokens.some(qt => nameTokens.includes(qt));
      if (hasOverlap) {
        results.push(name);
        continue;
      }

      // Levenshtein edit distance <= 2
      if (Math.abs(queryNorm.length - nameNorm.length) <= 2 && this.levenshtein(queryNorm, nameNorm) <= 2) {
        results.push(name);
      }
    }

    return [...new Set(results)];
  }

  private tokenize(text: string): string[] {
    const stopWords = new Set(['and', 'or', 'the', 'for', 'in', 'of', 'to', 'a', 'an', '&']);
    return text
      .split(/[\s&+/,_.-]+/)
      .map(t => t.toLowerCase().trim())
      .filter(t => t.length >= 2 && !stopWords.has(t));
  }

  private levenshtein(s: string, t: string): number {
    const n = s.length;
    const m = t.length;
    if (n === 0) return m;
    if (m === 0) return n;

    const d: number[][] = Array.from({ length: n + 1 }, () => new Array(m + 1).fill(0));

    for (let i = 0; i <= n; i++) d[i][0] = i;
    for (let j = 0; j <= m; j++) d[0][j] = j;

    for (let i = 1; i <= n; i++) {
      for (let j = 1; j <= m; j++) {
        const cost = s[i - 1] === t[j - 1] ? 0 : 1;
        d[i][j] = Math.min(
          d[i - 1][j] + 1,
          d[i][j - 1] + 1,
          d[i - 1][j - 1] + cost
        );
      }
    }
    return d[n][m];
  }
}
