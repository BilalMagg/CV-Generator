import {
  Component,
  Input,
  Output,
  EventEmitter,
  forwardRef,
  ElementRef,
  HostListener,
  signal,
  computed,
  ContentChildren,
  QueryList,
  AfterContentInit,
  booleanAttribute,
  inject,
  ChangeDetectorRef,
  TemplateRef,
  ContentChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';

export interface SelectOption<T = any> {
  value: T;
  label: string;
  icon?: string;
  description?: string;
  disabled?: boolean;
  className?: string;
}

/**
 * <ui-select-item [value]="val" [disabled]="bool">
 * Single item inside the dropdown popover.
 */
@Component({
  selector: 'ui-select-item',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div
      class="ui-select-item-inner"
      [class.selected]="isSelected()"
      [class.focused]="isFocused()"
      [class.disabled]="disabled"
      role="option"
      [attr.aria-selected]="isSelected()"
      [attr.aria-disabled]="disabled"
      (click)="onClick($event)"
      (mouseenter)="onMouseEnter()"
    >
      <div class="ui-select-item-content">
        <ng-content></ng-content>
      </div>

      <span class="ui-select-item-check" *ngIf="isSelected()">
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="20 6 9 17 4 12"></polyline>
        </svg>
      </span>
    </div>
  `,
  styleUrl: './select.component.scss'
})
export class UiSelectItemComponent {
  @Input() value: any;
  @Input() label?: string;
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input() className = '';

  isSelected = signal(false);
  isFocused = signal(false);

  selectComponent?: UiSelectComponent;

  onClick(event: MouseEvent) {
    event.stopPropagation();
    if (this.disabled) return;
    this.selectComponent?.selectItem(this);
  }

  onMouseEnter() {
    if (this.disabled) return;
    this.selectComponent?.setFocusedItem(this);
  }

  getLabel(): string {
    if (this.label) return this.label;
    const text = this.el.nativeElement.textContent || '';
    return text.trim();
  }

  constructor(public el: ElementRef<HTMLElement>) {}
}

/**
 * <ui-select-group [label]="'Section Title'">
 * Optional grouping header with separator.
 */
@Component({
  selector: 'ui-select-group',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="ui-select-group-header" *ngIf="label">
      {{ label }}
    </div>
    <div class="ui-select-group-items">
      <ng-content></ng-content>
    </div>
  `,
  styleUrl: './select.component.scss'
})
export class UiSelectGroupComponent {
  @Input() label = '';
}

/**
 * <ui-select>
 * Modern Radix/shadcn-inspired custom dropdown component.
 */
@Component({
  selector: 'ui-select',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UiSelectComponent),
      multi: true
    }
  ],
  template: `
    <div
      class="ui-select-container"
      [class.open]="isOpen()"
      [class.disabled]="disabled"
      [class.is-invalid]="isInvalid"
      [class]="className"
    >
      <!-- Trigger Button -->
      <button
        type="button"
        class="ui-select-trigger"
        [id]="id"
        [disabled]="disabled"
        aria-haspopup="listbox"
        [attr.aria-expanded]="isOpen()"
        (click)="toggleOpen($event)"
        (keydown)="onKeydown($event)"
      >
        <span class="ui-select-value" [class.placeholder]="!hasSelection()">
          {{ displayLabel() }}
        </span>

        <span class="ui-select-chevron" aria-hidden="true">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <polyline points="6 9 12 15 18 9"></polyline>
          </svg>
        </span>
      </button>

      <!-- Dropdown Popover Content -->
      <div
        class="ui-select-popover"
        *ngIf="isOpen()"
        role="listbox"
        (click)="$event.stopPropagation()"
      >
        <!-- Search filter (visible if searchable or > 10 options) -->
        <div class="ui-select-search-wrap" *ngIf="searchable || (items() && items().length > 10)">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="search-icon">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <input
            type="text"
            class="ui-select-search-input"
            [placeholder]="searchPlaceholder"
            [(ngModel)]="searchQuery"
            (ngModelChange)="onSearchChange($event)"
            (keydown)="onSearchKeydown($event)"
            #searchInput
          />
        </div>

        <div class="ui-select-items-list" #itemsList>
          <!-- When options array is supplied -->
          @if (items() && items().length > 0) {
            @for (opt of filteredOptions(); track opt.value) {
              <div
                class="ui-select-item-inner"
                [class.selected]="opt.value === currentValue()"
                [class.disabled]="opt.disabled"
                role="option"
                [attr.aria-selected]="opt.value === currentValue()"
                (click)="selectOption(opt, $event)"
              >
                <div class="ui-select-item-content">
                  <i *ngIf="opt.icon" [class]="opt.icon + ' opt-icon'"></i>
                  <span class="opt-label" [class]="opt.className || ''">{{ opt.label }}</span>
                  <span *ngIf="opt.description" class="opt-desc">{{ opt.description }}</span>
                </div>

                <span class="ui-select-item-check" *ngIf="opt.value === currentValue()">
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                </span>
              </div>
            }
            @if (filteredOptions().length === 0) {
              <div class="ui-select-empty">No matching options</div>
            }
          }

          <!-- Projected <ui-select-item> children -->
          <ng-content></ng-content>
        </div>
      </div>
    </div>
  `,
  styleUrl: './select.component.scss'
})
export class UiSelectComponent implements ControlValueAccessor, AfterContentInit {
  private el = inject(ElementRef);
  private cdr = inject(ChangeDetectorRef);

  @Input() id = '';
  @Input() placeholder = 'Select an option…';
  @Input({ transform: booleanAttribute }) disabled = false;
  @Input({ transform: booleanAttribute }) isInvalid = false;
  @Input({ transform: booleanAttribute }) searchable = false;
  @Input() searchPlaceholder = 'Search…';
  @Input() className = '';

  /** Direct options array support: string[] or SelectOption[] */
  @Input() set options(val: (string | SelectOption)[] | undefined) {
    if (!val) {
      this.items.set([]);
      this.filteredOptions.set([]);
      return;
    }
    const normalized: SelectOption[] = val.map(item => {
      if (typeof item === 'string') {
        return { value: item, label: item };
      }
      return item;
    });
    this.items.set(normalized);
    this.filteredOptions.set(normalized);
  }

  @Output() valueChange = new EventEmitter<any>();

  @ContentChildren(UiSelectItemComponent, { descendants: true })
  projectedItems?: QueryList<UiSelectItemComponent>;

  isOpen = signal(false);
  items = signal<SelectOption[]>([]);
  searchQuery = '';
  filteredOptions = signal<SelectOption[]>([]);

  currentValue = signal<any>(null);
  focusedIndex = -1;

  // ControlValueAccessor handlers
  private onChange: (val: any) => void = () => {};
  private onTouched: () => void = () => {};

  displayLabel = computed(() => {
    const val = this.currentValue();
    if (val === null || val === undefined || val === '') {
      return this.placeholder;
    }

    // Check in options array
    const foundInOptions = this.items().find(o => o.value === val);
    if (foundInOptions) return foundInOptions.label;

    // Check in projected items
    if (this.projectedItems) {
      const match = this.projectedItems.find(item => item.value === val);
      if (match) return match.getLabel();
    }

    return String(val);
  });

  hasSelection = computed(() => {
    const val = this.currentValue();
    return val !== null && val !== undefined && val !== '';
  });

  ngAfterContentInit() {
    this.filteredOptions.set(this.items());

    if (this.projectedItems) {
      this.linkProjectedItems();
      this.projectedItems.changes.subscribe(() => {
        this.linkProjectedItems();
        this.updateItemSelectionState();
      });
    }
  }

  private linkProjectedItems() {
    if (!this.projectedItems) return;
    this.projectedItems.forEach(item => {
      item.selectComponent = this;
    });
    this.updateItemSelectionState();
  }

  private updateItemSelectionState() {
    if (!this.projectedItems) return;
    const val = this.currentValue();
    this.projectedItems.forEach(item => {
      item.isSelected.set(item.value === val);
    });
  }

  toggleOpen(event?: MouseEvent) {
    if (event) event.stopPropagation();
    if (this.disabled) return;

    if (this.isOpen()) {
      this.close();
    } else {
      this.open();
    }
  }

  open() {
    if (this.disabled) return;
    this.isOpen.set(true);
    this.searchQuery = '';
    this.filteredOptions.set(this.items());
    this.onTouched();
    this.cdr.markForCheck();
  }

  close() {
    if (!this.isOpen()) return;
    this.isOpen.set(false);
    this.searchQuery = '';
    this.cdr.markForCheck();
  }

  selectOption(opt: SelectOption, event?: MouseEvent) {
    if (event) event.stopPropagation();
    if (opt.disabled) return;

    this.setValue(opt.value);
    this.close();
  }

  selectItem(item: UiSelectItemComponent) {
    if (item.disabled) return;
    this.setValue(item.value);
    this.close();
  }

  setFocusedItem(item: UiSelectItemComponent) {
    if (this.projectedItems) {
      this.projectedItems.forEach(i => i.isFocused.set(i === item));
    }
  }

  setValue(val: any) {
    this.currentValue.set(val);
    this.onChange(val);
    this.valueChange.emit(val);
    this.updateItemSelectionState();
    this.cdr.markForCheck();
  }

  onSearchChange(query: string) {
    const q = (query || '').toLowerCase().trim();
    if (!q) {
      this.filteredOptions.set(this.items());
      return;
    }
    const filtered = this.items().filter(opt =>
      opt.label.toLowerCase().includes(q) ||
      (opt.description && opt.description.toLowerCase().includes(q))
    );
    this.filteredOptions.set(filtered);
  }

  onSearchKeydown(event: KeyboardEvent) {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const first = this.filteredOptions()[0];
      if (first) {
        this.selectOption(first);
      }
    }
  }

  onKeydown(event: KeyboardEvent) {
    if (this.disabled) return;

    if (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown') {
      if (!this.isOpen()) {
        event.preventDefault();
        this.open();
      }
    } else if (event.key === 'Escape') {
      if (this.isOpen()) {
        event.preventDefault();
        this.close();
      }
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent) {
    if (!this.isOpen()) return;
    const target = event.target as HTMLElement;
    if (!this.el.nativeElement.contains(target)) {
      this.close();
    }
  }

  // ControlValueAccessor methods
  writeValue(value: any): void {
    this.currentValue.set(value);
    this.updateItemSelectionState();
    this.cdr.markForCheck();
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  setDisabledState?(isDisabled: boolean): void {
    this.disabled = isDisabled;
    this.cdr.markForCheck();
  }
}

export const UI_SELECT_COMPONENTS = [
  UiSelectComponent,
  UiSelectItemComponent,
  UiSelectGroupComponent
] as const;
