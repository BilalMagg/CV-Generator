import { Injectable, inject } from '@angular/core';
import { HttpService } from '@app/services/http.service';
import { ApiResponse } from '@app/models/application.model';
import { ENTITY_FIELDS, type EntityType } from '@app/models/user-content.models';

export type ExportKind = 'userContent' | 'companies' | 'contacts';
export type ExportFormat = 'json' | 'csv' | 'excel';

export interface ExportGroupDef {
  key: string;
  label: string;
  kind: ExportKind;
}

export interface ExportRequest {
  def: ExportGroupDef;
  items: any[];
  selectedItemIds: Set<string>;
  selectedFields: Set<string>;
}

/** Every exportable group. User-content entities reuse GET /api/user-content/{key}. */
export const EXPORT_GROUPS: ExportGroupDef[] = [
  { key: 'cvprofiles', label: 'CV Profiles', kind: 'userContent' },
  { key: 'projects', label: 'Projects', kind: 'userContent' },
  { key: 'skills', label: 'Skills', kind: 'userContent' },
  { key: 'experiences', label: 'Experiences', kind: 'userContent' },
  { key: 'educations', label: 'Educations', kind: 'userContent' },
  { key: 'certifications', label: 'Certifications', kind: 'userContent' },
  { key: 'languages', label: 'Languages', kind: 'userContent' },
  { key: 'interests', label: 'Interests', kind: 'userContent' },
  { key: 'sociallinks', label: 'Social Links', kind: 'userContent' },
  { key: 'academicactivities', label: 'Academic Activities', kind: 'userContent' },
  { key: 'hackathons', label: 'Hackathons', kind: 'userContent' },
  { key: 'companies', label: 'Companies', kind: 'companies' },
  { key: 'contacts', label: 'Contacts', kind: 'contacts' },
];

@Injectable({ providedIn: 'root' })
export class ExportService {
  private readonly http = inject(HttpService);

  /** Fetch the raw items for a group (unwrapping the ApiResponse envelope). */
  async fetchGroup(def: ExportGroupDef): Promise<any[]> {
    if (def.kind === 'userContent') {
      const res = await this.http.get<ApiResponse<any[]>>(`/api/user-content/${def.key}`);
      return res.data ?? [];
    }
    if (def.kind === 'companies') {
      const res = await this.http.get<ApiResponse<{ items: any[] }>>('/api/companies');
      return res.data?.items ?? [];
    }
    const res = await this.http.get<ApiResponse<{ items: any[] }>>('/api/contacts');
    return res.data?.items ?? [];
  }

  /** Available column/field names for a group (id first). */
  columnsFor(def: ExportGroupDef, sample: any): string[] {
    if (def.kind === 'userContent') {
      const fields = ENTITY_FIELDS[def.key as EntityType] ?? [];
      const names = fields.map((f) => f.name);
      if (sample && 'id' in sample && !names.includes('id')) names.unshift('id');
      return names;
    }
    const keys = sample ? Object.keys(sample) : [];
    if (!keys.includes('id')) keys.unshift('id');
    return keys;
  }

  fieldLabel(def: ExportGroupDef, field: string): string {
    if (def.kind === 'userContent') {
      const f = (ENTITY_FIELDS[def.key as EntityType] ?? []).find((x) => x.name === field);
      if (f) return f.label;
    }
    return humanize(field);
  }

  async build(requests: ExportRequest[], format: ExportFormat): Promise<void> {
    const prepared = requests
      .map((r) => {
        const items = r.items.filter((it) => r.selectedItemIds.has(it?.id));
        const fields = Array.from(r.selectedFields);
        const rows = items.map((it) => {
          const o: Record<string, unknown> = {};
          for (const f of fields) o[this.fieldLabel(r.def, f)] = serialize(it?.[f]);
          return o;
        });
        return { def: r.def, rows };
      })
      .filter((p) => p.rows.length > 0);

    if (prepared.length === 0) return;

    const stamp = new Date().toISOString().slice(0, 10);
    if (format === 'json') {
      const obj: Record<string, unknown> = {};
      for (const p of prepared) obj[p.def.label] = p.rows;
      this.download(new Blob([JSON.stringify(obj, null, 2)], { type: 'application/json' }), `export-${stamp}.json`);
      return;
    }

    if (format === 'excel') {
      const XLSX = await import('xlsx');
      const wb = XLSX.utils.book_new();
      for (const p of prepared) {
        const ws = XLSX.utils.json_to_sheet(p.rows as any);
        XLSX.utils.book_append_sheet(wb, ws, sheetName(p.def.label));
      }
      const buf = XLSX.write(wb, { type: 'array', bookType: 'xlsx' });
      this.download(new Blob([buf], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }), `export-${stamp}.xlsx`);
      return;
    }

    // CSV (one file per group; zipped when multiple)
    const csvs = prepared.map((p) => ({ name: p.def.label, csv: toCsv(p.rows) }));
    if (csvs.length === 1) {
      this.download(new Blob([csvs[0].csv], { type: 'text/csv' }), `${safeFile(csvs[0].name)}.csv`);
      return;
    }
    const JSZip = (await import('jszip')).default;
    const zip = new JSZip();
    for (const c of csvs) zip.file(`${safeFile(c.name)}.csv`, c.csv);
    const blob = await zip.generateAsync({ type: 'blob' });
    this.download(blob, `export-${stamp}.zip`);
  }

  private download(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
}

function serialize(v: unknown): unknown {
  if (v === null || v === undefined) return '';
  if (typeof v === 'object') return JSON.stringify(v);
  return v;
}

function humanize(field: string): string {
  const s = field.replace(/([A-Z])/g, ' $1').replace(/[_-]/g, ' ').trim();
  return s.charAt(0).toUpperCase() + s.slice(1);
}

function safeFile(name: string): string {
  return name.replace(/[^a-z0-9]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase() || 'export';
}

function sheetName(label: string): string {
  const s = label.replace(/[^a-z0-9 ]/gi, '').slice(0, 31);
  return s || 'Sheet';
}

function toCsv(rows: Record<string, unknown>[]): string {
  if (rows.length === 0) return '';
  const headers = Object.keys(rows[0]);
  const esc = (v: unknown) => {
    const s = v === null || v === undefined ? '' : String(v);
    return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const lines = [headers.map(esc).join(',')];
  for (const r of rows) lines.push(headers.map((h) => esc(r[h])).join(','));
  return lines.join('\r\n');
}
