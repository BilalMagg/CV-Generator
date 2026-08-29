import { Component, effect, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  ExportService,
  EXPORT_GROUPS,
  type ExportFormat,
  type ExportGroupDef,
  type ExportRequest,
} from '@app/services/export.service';

interface GroupUi {
  def: ExportGroupDef;
  checked: boolean;
  loading: boolean;
  items: any[];
  selectedItemIds: Set<string>;
  fields: string[];
  selectedFields: Set<string>;
  search: string;
}

@Component({
  selector: 'app-export-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './export-dialog.component.html',
  styleUrl: './export-dialog.component.scss',
})
export class ExportDialogComponent {
  singleEntity = input<string | undefined>(undefined);
  open = model.required<boolean>();
  close = output<void>();

  private readonly exportService = inject(ExportService);

  format = signal<ExportFormat>('json');
  ui = signal<GroupUi[]>([]);
  busy = signal(false);

  constructor() {
    effect(() => {
      if (this.open()) void this.loadAll();
    });
  }

  get groups(): ExportGroupDef[] {
    const single = this.singleEntity();
    return single ? EXPORT_GROUPS.filter((g) => g.key === single) : EXPORT_GROUPS;
  }

  private async loadAll(): Promise<void> {
    const groups = this.groups;
    const loaded = await Promise.all(
      groups.map(async (def) => {
        try {
          const items = await this.exportService.fetchGroup(def);
          const fields = this.exportService.columnsFor(def, items[0] ?? {});
          return {
            def,
            checked: true,
            loading: false,
            items,
            selectedItemIds: new Set(items.map((i) => i?.id).filter(Boolean)),
            fields,
            selectedFields: new Set(fields),
            search: '',
          } as GroupUi;
        } catch {
          return {
            def,
            checked: false,
            loading: false,
            items: [],
            selectedItemIds: new Set<string>(),
            fields: [],
            selectedFields: new Set<string>(),
            search: '',
          } as GroupUi;
        }
      }),
    );
    this.ui.set(loaded);
  }

  private patch(key: string, fn: (g: GroupUi) => GroupUi): void {
    this.ui.update((list) => list.map((g) => (g.def.key === key ? fn(g) : g)));
  }

  group(key: string): GroupUi | undefined {
    return this.ui().find((g) => g.def.key === key);
  }

  visibleItems(g: GroupUi): any[] {
    const q = g.search.trim().toLowerCase();
    if (!q) return g.items;
    return g.items.filter((it) =>
      g.fields.some((f) => String(it?.[f] ?? '').toLowerCase().includes(q)),
    );
  }

  toggleGroup(g: GroupUi, checked: boolean): void {
    this.patch(g.def.key, (x) => ({ ...x, checked }));
  }

  toggleField(g: GroupUi, field: string, on: boolean): void {
    const set = new Set(g.selectedFields);
    if (on) set.add(field);
    else set.delete(field);
    this.patch(g.def.key, (x) => ({ ...x, selectedFields: set }));
  }

  toggleAllFields(g: GroupUi, on: boolean): void {
    this.patch(g.def.key, (x) => ({ ...x, selectedFields: on ? new Set(x.fields) : new Set<string>() }));
  }

  toggleItem(g: GroupUi, id: string, on: boolean): void {
    const set = new Set(g.selectedItemIds);
    if (on) set.add(id);
    else set.delete(id);
    this.patch(g.def.key, (x) => ({ ...x, selectedItemIds: set }));
  }

  toggleAllItems(g: GroupUi, on: boolean): void {
    const ids = this.visibleItems(g).map((i) => i?.id).filter(Boolean) as string[];
    const set = new Set(on ? ids : []);
    this.patch(g.def.key, (x) => ({ ...x, selectedItemIds: set }));
  }

  allFieldsOn(g: GroupUi): boolean {
    return g.fields.length > 0 && g.selectedFields.size === g.fields.length;
  }

  allItemsOn(g: GroupUi): boolean {
    const visible = this.visibleItems(g);
    return visible.length > 0 && visible.every((i) => g.selectedItemIds.has(i?.id));
  }

  get selectedGroups(): number {
    return this.ui().filter((g) => g.checked).length;
  }

  get selectedItems(): number {
    return this.ui()
      .filter((g) => g.checked)
      .reduce((n, g) => n + g.selectedItemIds.size, 0);
  }

  get canExport(): boolean {
    return this.selectedGroups > 0 && this.selectedItems > 0;
  }

  fieldLabel(g: GroupUi, field: string): string {
    return this.exportService.fieldLabel(g.def, field);
  }

  async doExport(): Promise<void> {
    if (!this.canExport) return;
    this.busy.set(true);
    const requests: ExportRequest[] = this.ui()
      .filter((g) => g.checked)
      .map((g) => ({
        def: g.def,
        items: g.items,
        selectedItemIds: g.selectedItemIds,
        selectedFields: g.selectedFields,
      }));
    try {
      await this.exportService.build(requests, this.format());
      this.closeModal();
    } finally {
      this.busy.set(false);
    }
  }

  closeModal(): void {
    this.open.set(false);
    this.close.emit();
  }
}
