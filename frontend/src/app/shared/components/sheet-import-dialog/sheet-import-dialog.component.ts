import { Component, inject, input, model, output, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpService } from '@app/services/http.service';
import { ApiResponse } from '@app/models/application.model';
import { ToastService } from '@app/services/toast.service';
import { ENTITY_FIELDS, type EntityType } from '@app/models/user-content.models';

export type ImportType =
  | 'companies'
  | 'contacts'
  | 'applications'
  | 'cvprofiles'
  | 'projects'
  | 'skills'
  | 'experiences'
  | 'educations'
  | 'certifications'
  | 'languages'
  | 'interests'
  | 'sociallinks'
  | 'academicactivities'
  | 'hackathons';

interface ImportResult {
  imported: number;
  skipped: number;
  errors: string[];
}

interface SheetAnalysis {
  rows: Record<string, unknown>[];
  mappedColumns: string[];
}

const BASE_HEADER_MAP: Record<string, Record<string, string>> = {
  companies: {
    company: 'name',
    name: 'name',
    companylink: 'websiteUrl',
    websiteurl: 'websiteUrl',
    website: 'websiteUrl',
    location: 'location',
    locationlink: 'locationUrl',
    note: 'note',
    notes: 'note',
  },
  contacts: {
    company: 'company',
    name: 'name',
    role: 'role',
    position: 'role',
    contactinfo: 'contactInfo',
    email: 'contactInfo',
    phone: 'contactInfo',
  },
  applications: {
    id: 'externalId',
    appid: 'externalId',
    company: 'company',
    position: 'position',
    internship: 'internshipType',
    internshiptype: 'internshipType',
    sourcetype: 'sourceType',
    source: 'sourceName',
    contactname: 'contactName',
    currentstatus: 'status',
    status: 'status',
    priority: 'priority',
    applydate: 'applyDate',
    cvversion: 'cvUrl',
    cvlink: 'cvUrl',
    motivationletter: 'motivationLetterSent',
    portfoliosent: 'portfolioSent',
    shouldiapplyagain: 'shouldApplyAgain',
    note: 'notes',
    notes: 'notes',
  },
};

/** Per-entity header maps derived from ENTITY_FIELDS so any My Career entity is importable. */
function buildUserContentHeaderMaps(): Record<string, Record<string, string>> {
  const out: Record<string, Record<string, string>> = {};
  (Object.keys(ENTITY_FIELDS) as EntityType[]).forEach((t) => {
    const m: Record<string, string> = {};
    for (const f of ENTITY_FIELDS[t]) {
      m[normalizeHeader(f.label)] = f.name;
      m[normalizeHeader(f.name)] = f.name;
    }
    out[t] = m;
  });
  return out;
}

const HEADER_MAP: Record<string, Record<string, string>> = {
  ...BASE_HEADER_MAP,
  ...buildUserContentHeaderMaps(),
};

const BOOL_FIELDS = new Set(['motivationLetterSent', 'portfolioSent']);

/** Pure — safe to call during template rendering. */
function analyzeSheet(text: string, type: ImportType): SheetAnalysis {
  const trimmed = text.trim();
  if (!trimmed) return { rows: [], mappedColumns: [] };

  const lines = trimmed.split(/\r?\n/).filter(l => l.trim().length > 0);
  if (lines.length < 2) return { rows: [], mappedColumns: [] };

  const sep = lines[0].includes('\t') ? '\t' : ',';
  const headers = splitLine(lines[0], sep);
  const map = HEADER_MAP[type];

  const colFields: (string | null)[] = headers.map(h => {
    const key = map[normalizeHeader(h)];
    return key ?? null;
  });
  const mappedColumns = colFields.filter((f): f is string => f !== null);
  if (mappedColumns.length === 0) return { rows: [], mappedColumns: [] };

  const rows: Record<string, unknown>[] = [];
  for (const line of lines.slice(1)) {
    const cells = splitLine(line, sep);
    const row: Record<string, unknown> = {};
    let hasValue = false;
    cells.forEach((cell, i) => {
      const field = colFields[i];
      if (!field) return;
      const value = cell.trim();
      if (value.length === 0) return;
      hasValue = true;
      row[field] = BOOL_FIELDS.has(field) ? /^(true|yes|y|1|sent)$/i.test(value) : value;
    });
    // Keep rows that carry at least one recognized value.
    if (hasValue) rows.push(row);
  }

  return { rows, mappedColumns };
}

function normalizeHeader(h: string): string {
  return h.trim().toLowerCase().replace(/[^a-z]/g, '');
}

/** Splits a line honoring double-quoted CSV segments; TSV splits are plain. */
function splitLine(line: string, sep: string): string[] {
  if (sep === '\t') return line.split('\t');
  const out: string[] = [];
  let cur = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (ch === '"') {
      if (inQuotes && line[i + 1] === '"') { cur += '"'; i++; }
      else inQuotes = !inQuotes;
    } else if (ch === ',' && !inQuotes) {
      out.push(cur); cur = '';
    } else {
      cur += ch;
    }
  }
  out.push(cur);
  return out;
}

@Component({
  selector: 'app-sheet-import-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="backdrop" (click)="close()">
        <div class="dialog" (click)="$event.stopPropagation()">
          <div class="head">
            <h2>Import {{ type() }}</h2>
            <button class="x" (click)="close()">✕</button>
          </div>

          @if (!result()) {
            <div class="body">
              <p class="hint">
                Pick a file or paste rows <strong>including the header row</strong>.
                Columns are matched by their header names; duplicates and invalid entries are skipped.
              </p>

              <div class="source-row">
                <label class="btn ghost file-label">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/></svg>
                  {{ fileLabel() }}
                  <input type="file" accept=".csv,.tsv,.txt,.xlsx,.xls" (change)="onFile($event)" hidden />
                </label>
                @if (fileName()) {
                  <button class="x file-clear" title="Clear" (click)="clearFile()">✕</button>
                }
              </div>

              <textarea
                rows="10"
                [ngModel]="raw()"
                (ngModelChange)="raw.set($event)"
                placeholder="…or paste rows here (Ctrl+V)"></textarea>

              @if (analysis(); as a) {
                @if (a.rows.length === 0 && raw().trim()) {
                  <div class="preview bad">
                    ⚠ No importable rows detected — check that the first pasted line contains the
                    column headers (e.g. Company, Location, Note).
                  </div>
                } @else if (a.rows.length > 0) {
                  <div class="preview">
                    ✓ <strong>{{ a.rows.length }}</strong> row{{ a.rows.length === 1 ? '' : 's' }} ready ·
                    columns matched: {{ a.mappedColumns.join(', ') }}
                  </div>
                }
              }
            </div>
            <div class="foot">
              <button class="btn ghost" (click)="close()">Cancel</button>
              <button class="btn primary" [disabled]="!canImport() || busy()" (click)="doImport()">
                {{ busy() ? 'Importing…' : importLabel() }}
              </button>
            </div>
          } @else {
            <div class="body">
              <div class="result-grid">
                <div class="res ok"><span class="num">{{ result()!.imported }}</span>imported</div>
                <div class="res skip"><span class="num">{{ result()!.skipped }}</span>skipped</div>
                @if (result()!.errors.length > 0) {
                  <div class="res err"><span class="num">{{ result()!.errors.length }}</span>errors</div>
                }
              </div>
              @if (result()!.errors.length > 0) {
                <ul class="err-list">
                  @for (e of result()!.errors.slice(0, 8); track e) {
                    <li>{{ e }}</li>
                  }
                  @if (result()!.errors.length > 8) {
                    <li>…and {{ result()!.errors.length - 8 }} more</li>
                  }
                </ul>
              }
            </div>
            <div class="foot">
              <button class="btn ghost" (click)="resetForAnother()">Import another</button>
              <button class="btn primary" (click)="close()">Done</button>
            </div>
          }
        </div>
      </div>
    }
  `,
  styles: `
    .backdrop { position: fixed; inset: 0; background: rgb(0 0 0 / 35%); display: flex; align-items: center; justify-content: center; z-index: 200; }
    .dialog { width: 640px; max-width: calc(100vw - 32px); background: var(--surface, #fff); border-radius: 14px; box-shadow: 0 20px 60px rgb(0 0 0 / 25%); overflow: hidden; }
    .head { display: flex; justify-content: space-between; align-items: center; padding: 18px 20px 12px; }
    h2 { margin: 0; font-size: 16px; font-weight: 700; text-transform: capitalize; color: var(--text); }
    .x { border: none; background: transparent; cursor: pointer; color: var(--text-3); font-size: 14px; padding: 4px 8px; border-radius: 6px; }
    .x:hover { background: oklch(0.95 0.01 250); color: var(--text); }
    .body { padding: 4px 20px 8px; display: grid; gap: 12px; }
    .hint { margin: 0; font-size: 13px; color: var(--text-2); line-height: 1.5; }
    .source-row { display: flex; align-items: center; gap: 8px; }
    .file-label { cursor: pointer; display: inline-flex; align-items: center; gap: 7px; max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    textarea { width: 100%; border: 1px solid var(--border); border-radius: 8px; padding: 10px 12px; font: 12px/1.5 ui-monospace, monospace; resize: vertical; outline: none; color: var(--text); background: var(--bg, #fff); box-sizing: border-box; }
    textarea:focus { border-color: oklch(0.55 0.2 260); }
    .preview { font-size: 13px; color: oklch(0.5 0.14 150); }
    .preview.bad { color: oklch(0.55 0.2 25); }
    .foot { display: flex; justify-content: flex-end; gap: 10px; padding: 12px 20px 18px; }
    .btn { padding: 8px 16px; border-radius: 8px; font: inherit; font-size: 13px; font-weight: 600; cursor: pointer; border: none; display: inline-flex; align-items: center; justify-content: center; }
    .btn.primary { background: oklch(0.55 0.2 260); color: #fff; }
    .btn.primary:disabled { opacity: .5; cursor: default; }
    .btn.ghost { background: transparent; border: 1px solid var(--border); color: var(--text-2); font-weight: 400; }
    .result-grid { display: flex; gap: 24px; padding: 8px 0; }
    .res { display: grid; gap: 2px; font-size: 12px; color: var(--text-3); }
    .res .num { font-size: 26px; font-weight: 700; color: var(--text); }
    .res.ok .num { color: oklch(0.55 0.15 150); }
    .res.err .num { color: oklch(0.55 0.2 25); }
    .err-list { margin: 0; padding-left: 18px; font-size: 12.5px; color: var(--text-2); max-height: 140px; overflow-y: auto; }
  `,
})
export class SheetImportDialogComponent {
  private readonly http = inject(HttpService);
  private readonly toast = inject(ToastService);

  type = input.required<ImportType>();
  open = model.required<boolean>();
  imported = output<void>();

  raw = signal('');
  fileName = signal('');
  busy = signal(false);
  result = signal<ImportResult | null>(null);

  /** Pure derived state — no writes during render. */
  protected analysis = computed(() => {
    if (!this.raw().trim()) return null;
    return analyzeSheet(this.raw(), this.type());
  });

  canImport(): boolean {
    return (this.analysis()?.rows.length ?? 0) > 0;
  }

  importLabel(): string {
    const n = this.analysis()?.rows.length ?? 0;
    return n > 0 ? `Import ${n.toLocaleString()} row${n === 1 ? '' : 's'}` : 'Import';
  }

  fileLabel(): string {
    return this.fileName() || 'Choose file (CSV, XLSX…)';
  }

  async onFile(ev: Event) {
    const inputEl = ev.target as HTMLInputElement;
    const file = inputEl.files?.[0];
    inputEl.value = '';
    if (!file) return;

    try {
      let text: string;
      if (/\.(xlsx|xls)$/i.test(file.name)) {
        this.busy.set(true);
        const XLSX = await import('xlsx');
        const buf = await file.arrayBuffer();
        const wb = XLSX.read(buf, { type: 'array' });
        const firstSheet = wb.SheetNames[0];
        if (!firstSheet) throw new Error('empty workbook');
        text = XLSX.utils.sheet_to_csv(wb.Sheets[firstSheet]);
        this.busy.set(false);
      } else {
        text = await file.text();
      }

      this.result.set(null);
      this.fileName.set(file.name);
      this.raw.set(text);
    } catch {
      this.busy.set(false);
      this.toast.error('Could not read that file');
    }
  }

  clearFile() {
    this.fileName.set('');
    this.raw.set('');
  }

  resetForAnother() {
    this.raw.set('');
    this.fileName.set('');
    this.result.set(null);
  }

  close() {
    this.open.set(false);
    this.resetForAnother();
  }

  async doImport() {
    const rows = this.analysis()?.rows ?? [];
    if (rows.length === 0) return;
    this.busy.set(true);
    try {
      const res = await this.http.post<ApiResponse<ImportResult>>(`/api/imports/${this.type()}`, rows);
      this.result.set(res.data ?? { imported: 0, skipped: 0, errors: ['No response body'] });
      this.toast.success(`${res.data?.imported ?? 0} ${this.type()} imported`);
      this.imported.emit();
    } catch (err: unknown) {
      const msg = (err as { error?: { message?: string } })?.error?.message;
      this.toast.error(msg || 'Import failed');
    } finally {
      this.busy.set(false);
    }
  }
}
