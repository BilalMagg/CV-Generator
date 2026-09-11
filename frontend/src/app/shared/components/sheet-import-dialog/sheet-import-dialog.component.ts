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
  categoriesCreated?: number;
  createdCategories?: string[];
}

interface SheetAnalysis {
  rows: Record<string, unknown>[];
  mappedColumns: string[];
  requiredUnmapped: FieldDef[];
}

/** Per-category disposition chosen in the new-categories mapping popup. */
type CategoryPlanMode = 'create' | 'existing' | 'uncategorized';
interface CategoryPlanEntry {
  mode: CategoryPlanMode;
  /** Existing root category the row's category will be renamed to when mode='existing'. */
  targetName?: string;
}
const UNCATEGORIZED_KEY = '__uncat__';

export interface FieldDef {
  /** Backend/entity property the column feeds (e.g. 'name', 'sector'). */
  field: string;
  /** Human label shown in the mapping panel. */
  label: string;
  /** When true, the import is blocked until this field maps to a column. */
  required?: boolean;
  /** Input vintage from ENTITY_FIELDS (text/textarea/date/…). */
  type?: string;
  /** Guidance shown under the select (criteria for the column). */
  hint?: string;
  /** Header names (incl. label + common aliases) used for auto-mapping. */
  aliases?: string[];
}

const CATALOGS: Record<string, FieldDef[]> = {
  companies: [
    { field: 'name', label: 'Company name', required: true, hint: 'Company name — each row becomes one company.', aliases: ['name', 'company', 'companyname', 'societe', 'société', 'entreprise'] },
    { field: 'sector', label: 'Industry / Sector', hint: 'e.g. IT services, construction. Maps to the Sector field.', aliases: ['sector', 'industry', 'secteur', 'activite', 'activité', 'domain'] },
    { field: 'websiteUrl', label: 'Website', hint: 'A bare domain (serviclic.net) is auto-prefixed with https://.', aliases: ['websiteurl', 'website', 'siteweb', 'url', 'site'] },
    { field: 'linkedinUrl', label: 'LinkedIn URL', hint: 'e.g. linkedin.com/company/…', aliases: ['linkedinurl', 'linkedin', 'linkedinurl0', 'linkedinurlopen'] },
    { field: 'location', label: 'City / Locality', hint: 'City or locality only.', aliases: ['location', 'city', 'locality', 'ville'] },
    { field: 'region', label: 'Region', hint: 'e.g. Casablanca-Settat, Rabat-Salé-Kénitra.', aliases: ['region', 'région', 'administrativearea'] },
    { field: 'country', label: 'Country', hint: 'Defaults to Morocco when empty.', aliases: ['country', 'pays'] },
    { field: 'foundedYear', label: 'Founded year', hint: 'A 4-digit year (2023).', aliases: ['foundedyear', 'founded', 'creation', 'year'] },
    { field: 'size', label: 'Employee size', hint: 'Ranges like 10-50; compact codes (01-Oct) are decoded.', aliases: ['size', 'employees', 'effectif', 'effectifs', 'headcount'] },
    { field: 'locationUrl', label: 'Map link', hint: 'Google Maps / location URL.', aliases: ['locationurl', 'locationlink', 'maps', 'googlemaps'] },
    { field: 'note', label: 'Note', hint: 'Free-form memo.', aliases: ['note', 'notes', 'commentaire', 'remarque'] },
    { field: 'description', label: 'Description', hint: 'Longer company description (used in letter templates).', aliases: ['description', 'desc', 'about'] },
  ],
  contacts: [
    { field: 'name', label: 'Contact name', required: true, hint: 'A row needs a name and (an email or a phone) to be imported.', aliases: ['name', 'personne', 'nom', 'contact', 'fullname'] },
    { field: 'email', label: 'Email', hint: 'A row needs an email OR phone OR LinkedIn to be imported. Deduplicated by email.', aliases: ['email', 'mail', 'emailaddress', 'contactinfo', 'courriel'] },
    { field: 'phone', label: 'Phone (fixed)', hint: 'Landline — an email OR phone is required per row.', aliases: ['phone', 'tel', 'téléphone', 'telephone', 'fixe', 'landline'] },
    { field: 'mobile', label: 'Mobile / GSM', hint: 'Cell number.', aliases: ['mobile', 'gsm', 'portable', 'cellphone', 'cell'] },
    { field: 'fax', label: 'Fax', hint: 'Fax number.', aliases: ['fax', 'télécopie'] },
    { field: 'address', label: 'Address', hint: 'Street, building or city — anything location-like.', aliases: ['address', 'adresse', 'street', 'rue', 'ville'] },
    { field: 'company', label: 'Company', hint: 'Company/corporation the contact belongs to.', aliases: ['company', 'societe', 'société', 'entreprise', 'corporation'] },
    { field: 'role', label: 'Position / Role', hint: 'Job title in the company.', aliases: ['role', 'position', 'fonction', 'poste', 'title', 'titre'] },
    { field: 'linkedin', label: 'LinkedIn URL', hint: 'Profile URL — a row can be LinkedIn-only.', aliases: ['linkedin', 'linkedinurl', 'linkedink', 'profile', 'li'] },
  ],
  applications: [
    { field: 'company', label: 'Company', required: true, hint: 'Company this application targets.', aliases: ['company', 'societe', 'entreprise'] },
    { field: 'position', label: 'Position', hint: 'Job / internship title.', aliases: ['position', 'role', 'job', 'poste', 'title', 'intitulé'] },
    { field: 'internshipType', label: 'Internship type', hint: 'e.g. PFE, Summer, Observation.', aliases: ['internshiptype', 'internship', 'stage', 'type'] },
    { field: 'status', label: 'Status', hint: 'SAVED, APPLIED, SCREENING, INTERVIEW, OFFER, ACCEPTED, REJECTED, WITHDRAWN (also French/plain text).', aliases: ['status', 'currentstatus', 'etat', 'state'] },
    { field: 'priority', label: 'Priority', hint: 'LOW, MEDIUM, HIGH (plain text ok).', aliases: ['priority', 'priorite', 'priorité'] },
    { field: 'applyDate', label: 'Application date', hint: 'Day-first DD/MM/YYYY or ISO.', aliases: ['applydate', 'date', 'applied', 'applydateshh'] },
    { field: 'sourceType', label: 'Source type', hint: 'How the offer was found (REDDIT/LINKEDIN/…).', aliases: ['sourcetype', 'source'] },
    { field: 'sourceName', label: 'Source name', hint: 'Offer link or poster.', aliases: ['sourcename', 'poster', 'url', 'link', 'offerlink'] },
    { field: 'contactName', label: 'Contact name', hint: 'Matched to an existing contact if possible.', aliases: ['contactname', 'contact', 'recruiter'] },
    { field: 'externalId', label: 'External ID', hint: 'Your own reference (e.g. APP-001).', aliases: ['externalid', 'id', 'appid', 'ref'] },
    { field: 'cvUrl', label: 'CV link/version', hint: 'CV URL or version label.', aliases: ['cvurl', 'cvlink', 'cv', 'cvversion'] },
    { field: 'motivationLetterSent', label: 'Motivation letter', hint: 'true/yes/1/sent to flip the flag.', aliases: ['motivationletter', 'motivationlettersent', 'lm', 'coverletter'] },
    { field: 'portfolioSent', label: 'Portfolio sent', hint: 'true/yes/1/sent to flip the flag.', aliases: ['portfoliosent', 'portfolio', 'pf'] },
    { field: 'shouldApplyAgain', label: 'Re-apply?', hint: 'yes/no advice for re-applying.', aliases: ['shouldapplyagain', 'shouldiapplyagain', 'reapply'] },
    { field: 'notes', label: 'Notes', hint: 'Free text captured on the application.', aliases: ['notes', 'note', 'commentaire'] },
  ],
  skills: [
    { field: 'name', label: 'Skill name', required: true, hint: 'Skill name — each row becomes one skill; duplicates are skipped.', aliases: ['name', 'skill', 'skillname', 'competence', 'compétence', 'nom'] },
    { field: 'category', label: 'Category container', hint: 'e.g. Frontend, Cloud. Missing categories are created automatically.', aliases: ['category', 'categorie', 'catégorie', 'container'] },
    { field: 'level', label: 'Level', hint: 'Beginner, Intermediate, Advanced or Expert (case-insensitive).', aliases: ['level', 'niveau'] },
    { field: 'subcategory', label: 'Subcategory', hint: 'e.g. React, Node.js.', aliases: ['subcategory', 'souscategorie', 'sous-catégorie'] },
    { field: 'yearsOfExperience', label: 'Years of experience', hint: 'A number (0-60).', aliases: ['yearsofexperience', 'years', 'annees', 'années'] },
    { field: 'lastUsedYear', label: 'Last used year', hint: 'A 4-digit year.', aliases: ['lastusedyear', 'lastused', 'used', 'derniere'] },
    { field: 'isCore', label: 'Core skill', hint: 'true/yes/1 marks it as a core skill.', aliases: ['iscore', 'core', 'essential', 'prioritaire'] },
  ],
};

const BOOL_FIELDS = new Set(['motivationLetterSent', 'portfolioSent']);

function catalogFor(type: ImportType): FieldDef[] {
  const fixed = CATALOGS[type];
  if (fixed) return fixed;
  const fields = ENTITY_FIELDS[type as EntityType];
  if (fields?.length) {
    return fields.map(f => ({
      field: f.name,
      label: f.label,
      required: Boolean((f as { required?: boolean }).required),
      type: f.type,
      hint: (f as { placeholder?: string }).placeholder,
      aliases: [f.name, f.label],
    }));
  }
  return [];
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

function parseTable(text: string): { headers: string[]; sep: string; data: string[][] } | null {
  const trimmed = text.trim();
  if (!trimmed) return null;
  const lines = trimmed.split(/\r?\n/).filter(l => l.trim().length > 0);
  if (lines.length < 2) return null;
  const sep = lines[0].includes('\t') ? '\t' : ',';
  const headers = splitLine(lines[0], sep).map(h => h.trim());
  const data = lines.slice(1).map(l => splitLine(l, sep));
  return { headers, sep, data };
}

/** Builds rows from the active field→header mapping. */
function buildRows(text: string, type: ImportType, mapping: Record<string, string>): SheetAnalysis {
  const table = parseTable(text);
  const catalog = catalogFor(type);
  if (!table) return { rows: [], mappedColumns: [], requiredUnmapped: catalog.filter(d => d.required) };

  const { headers, data } = table;
  const colFields: (string | null)[] = headers.map(h => {
    for (const d of catalog) if (mapping[d.field] === h) return d.field;
    return null;
  });

  const mappedFields = new Set(colFields.filter((f): f is string => f !== null));
  const mappedColumns = catalog.filter(d => mappedFields.has(d.field)).map(d => d.label);
  const requiredUnmapped = catalog.filter(d => d.required && !mappedFields.has(d.field));
  const generic = catalog.length === 0;

  const rows: Record<string, unknown>[] = [];
  for (const cells of data) {
    const row: Record<string, unknown> = {};
    let hasValue = false;
    cells.forEach((cell, i) => {
      let field = colFields[i];
      if (!field && generic && i < headers.length) field = normalizeHeader(headers[i]);
      if (!field) return;
      const value = cell.trim();
      if (value.length === 0) return;
      hasValue = true;
      row[field] = BOOL_FIELDS.has(field) ? /^(true|yes|y|1|sent)$/i.test(value) : value;
    });
    if (hasValue) rows.push(row);
  }

  return { rows, mappedColumns, requiredUnmapped };
}

/** First non-empty cell of the first data row for a given header — sample to preview. */
function sampleValue(table: { headers: string[]; data: string[][] } | null, header: string): string {
  if (!table) return '';
  const i = table.headers.indexOf(header);
  if (i < 0) return '';
  for (const cells of table.data) {
    const v = (cells[i] ?? '').trim();
    if (v) return v.length > 40 ? v.slice(0, 40) + '…' : v;
  }
  return '(empty)';
}

function pickBestHeader(def: FieldDef, headers: string[], used: Set<string>): string | null {
  const aliases = new Set([def.field, def.label, ...(def.aliases ?? [])].map(normalizeHeader));
  // Pass 1: exact normalized match.
  for (const h of headers) {
    if (used.has(h)) continue;
    if (aliases.has(normalizeHeader(h))) return h;
  }
  // Pass 2: token containment (single-token aliases only).
  const single = [...aliases].filter(a => a.length > 2);
  for (const h of headers) {
    if (used.has(h)) continue;
    const nh = normalizeHeader(h);
    for (const a of single) {
      if (nh.includes(a) || a.includes(nh)) return h;
    }
  }
  return null;
}

@Component({
  selector: 'app-sheet-import-dialog',
  standalone: true,
  imports: [FormsModule],
  template: `
    @if (open()) {
      <div class="backdrop" (click)="close()">
        <div class="dialog" (click)="$event.stopPropagation()">
          @if (showCategoryMap()) {
            <div class="map-backdrop" (click)="closeCategoryMap()">
              <div class="map-dialog" (click)="$event.stopPropagation()">
                <h3>New categories</h3>
                <p class="hint">
                  Your sheet uses {{ categoryPlanList().length }} categor{{ categoryPlanList().length === 1 ? 'y' : 'ies' }}
                  that don't exist in your library yet. Choose what happens to each on import.
                </p>
                <div class="map-rows">
                  @for (entry of categoryPlanList(); track entry.name) {
                    <div class="map-row">
                      <div class="map-info">
                        <span class="chip">{{ entry.name }}</span>
                        <small class="mode-hint">{{ planModeHint(entry) }}</small>
                      </div>
                      <select
                        [ngModel]="planModeValue(entry)"
                        (ngModelChange)="setPlanMode(entry.name, $event)"
                      >
                        <option value="">Create new category</option>
                        <option [value]="uncatKey">Keep uncategorized</option>
                        @if (existingSkillNames().length > 0) {
                          <optgroup label="Move into existing category">
                            @for (existing of existingSkillNames(); track existing) {
                              <option [value]="existing">{{ existing }}</option>
                            }
                          </optgroup>
                        }
                      </select>
                    </div>
                  }
                </div>
                <div class="map-foot">
                  <button class="btn ghost" (click)="closeCategoryMap()">Cancel</button>
                  <button class="btn primary" (click)="applyPlan()">Apply & import {{ analysis().rows.length }} rows</button>
                </div>
              </div>
            </div>
          }
          <div class="head">
            <h2>Import {{ type() }}</h2>
            <button class="x" (click)="close()">✕</button>
          </div>

          @if (!result()) {
            <div class="body">
              <p class="hint">
                Pick a file or paste rows <strong>including the header row</strong>, then map the columns
                on the right — each field tells you what it needs. Duplicates and invalid entries are skipped.
              </p>

              <div class="workspace">
                <div class="pane">
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
                    rows="12"
                    [ngModel]="raw()"
                    (ngModelChange)="refreshOnEdit($event)"
                    placeholder="…or paste rows here (Ctrl+V)"></textarea>

                  @if (analysis(); as a) {
                    @if (a.rows.length === 0 && raw().trim()) {
                      <div class="preview bad">
                        ⚠ No importable rows detected — check that the first pasted line contains the
                        column headers and map at least one column on the right.
                      </div>
                    } @else if (a.rows.length > 0 || sourceHeaders().length > 0) {
                      <div class="preview">
                        ✓ <strong>{{ a.rows.length }}</strong> row{{ a.rows.length === 1 ? '' : 's' }} ready
                        @if (a.mappedColumns.length > 0) {
                          · columns matched: {{ a.mappedColumns.join(', ') }}
                        }
                      </div>
                    }
                  }

                  @if (requiredUnmapped(); as missing) {
                    @if (missing.length > 0) {
                      <div class="preview bad">
                        ⚠ Missing required column{{ missing.length > 1 ? 's' : '' }}:
                        {{ missing.map(m => m.label).join(', ') }}.
                      </div>
                    }
                  }

                  @if (type() === 'skills' && missingCategories().length > 0) {
                    <div class="preview bad">
                      (*) {{ missingCategories().join(', ') }} — not in your categories yet and will be created on import.
                    </div>
                  }
                </div>

                <aside class="map-pane">
                  @if (sourceHeaders().length === 0) {
                    <div class="map-empty">Paste rows above to map their columns.</div>
                  } @else {
                    <div class="map-head">
                      <h3>Header mapping</h3>
                      <button type="button" class="link-btn" (click)="autoMap()">Auto-map</button>
                      <button type="button" class="link-btn" (click)="clearMapping()">Clear</button>
                    </div>
                    <div class="map-fields">
                      @for (def of fieldCatalog(); track def.field) {
                        <div class="map-field" [class.required]="def.required">
                          <label>{{ def.label }} @if (def.required) {<span class="req" title="Required to import">*</span>}</label>
                          <select [ngModel]="fieldMapping(def.field)" (ngModelChange)="assignMapping(def.field, $event)">
                            <option value="">— skip —</option>
                            @for (h of sourceHeaders(); track h) {
                              <option [value]="h">{{ h }}</option>
                            }
                          </select>
                          @if (def.hint) {
                            <small class="hint">{{ def.hint }}</small>
                          }
                          <small class="sample">Sample: “{{ sampleOf(fieldMapping(def.field)) }}”</small>
                        </div>
                      }
                    </div>
                  }
                </aside>
              </div>
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
                @if (result()!.categoriesCreated) {
                  <div class="res ok"><span class="num">{{ result()!.categoriesCreated }}</span>categor{{ result()!.categoriesCreated === 1 ? 'y' : 'ies' }} created</div>
                }
                @if (result()!.errors.length > 0) {
                  <div class="res err"><span class="num">{{ result()!.errors.length }}</span>errors</div>
                }
              </div>
              @if (result()!.createdCategories; as created) {
                @if (created.length > 0) {
                  <p class="preview">Created: {{ created.join(', ') }}</p>
                }
              }
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
    .dialog { width: 1000px; max-width: calc(100vw - 32px); background: var(--surface, #fff); border-radius: 14px; box-shadow: 0 20px 60px rgb(0 0 0 / 25%); overflow: hidden; position: relative; }
    .map-backdrop { position: absolute; inset: 0; background: rgb(15 23 42 / 45%); backdrop-filter: blur(1px); display: flex; align-items: center; justify-content: center; z-index: 10; }
    .map-dialog { width: 540px; max-width: calc(100% - 48px); max-height: 85%; overflow-y: auto; background: var(--surface, #fff); border-radius: 12px; box-shadow: 0 20px 60px rgb(0 0 0 / 30%); padding: 18px 20px; display: grid; gap: 12px; box-sizing: border-box; }
    .map-dialog h3 { margin: 0; font-size: 15px; font-weight: 700; color: var(--text); }
    .map-rows { display: grid; gap: 12px; }
    .map-row { display: grid; gap: 6px; }
    .map-info { display: flex; align-items: baseline; gap: 8px; flex-wrap: wrap; }
    .chip { background: oklch(0.95 0.05 250); color: oklch(0.45 0.18 265); border-radius: 999px; padding: 3px 10px; font-size: 12px; font-weight: 600; white-space: nowrap; }
    .mode-hint { font-size: 11.5px; color: var(--text-3); }
    .map-foot { display: flex; justify-content: flex-end; gap: 10px; margin-top: 4px; }
    .head { display: flex; justify-content: space-between; align-items: center; padding: 18px 20px 12px; }
    h2 { margin: 0; font-size: 16px; font-weight: 700; text-transform: capitalize; color: var(--text); }
    .x { border: none; background: transparent; cursor: pointer; color: var(--text-3); font-size: 14px; padding: 4px 8px; border-radius: 6px; }
    .x:hover { background: oklch(0.95 0.01 250); color: var(--text); }
    .body { padding: 4px 20px 8px; display: grid; gap: 12px; }
    .hint { margin: 0; font-size: 13px; color: var(--text-2); line-height: 1.5; }
    .workspace { display: grid; grid-template-columns: minmax(0, 1fr) 380px; gap: 16px; }
    .pane { display: grid; gap: 10px; align-content: start; min-width: 0; }
    .source-row { display: flex; align-items: center; gap: 8px; }
    .file-label { cursor: pointer; display: inline-flex; align-items: center; gap: 7px; max-width: 420px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    textarea { width: 100%; border: 1px solid var(--border); border-radius: 8px; padding: 10px 12px; font: 12px/1.5 ui-monospace, monospace; resize: vertical; outline: none; color: var(--text); background: var(--bg, #fff); box-sizing: border-box; min-height: 220px; }
    textarea:focus { border-color: oklch(0.55 0.2 260); }
    .preview { font-size: 13px; color: oklch(0.5 0.14 150); }
    .preview.bad { color: oklch(0.55 0.2 25); }
    .map-pane { border-left: 1px solid var(--border); padding-left: 16px; max-height: 480px; overflow-y: auto; }
    .map-empty { font-size: 13px; color: var(--text-3); padding: 24px 8px; text-align: center; }
    .map-head { display: flex; align-items: center; gap: 8px; margin-bottom: 10px; }
    .map-head h3 { margin: 0; font-size: 13px; font-weight: 700; color: var(--text); flex: 1; }
    .link-btn { border: none; background: none; color: oklch(0.55 0.2 260); font: inherit; font-size: 12px; font-weight: 600; cursor: pointer; padding: 0; }
    .map-fields { display: grid; gap: 10px; }
    .map-field { display: grid; gap: 4px; }
    .map-field.required label { font-weight: 700; }
    .map-field label { font-size: 12.5px; color: var(--text); }
    .req { color: oklch(0.55 0.2 25); }
    select { border: 1px solid var(--border); border-radius: 6px; padding: 6px 8px; font: inherit; font-size: 12.5px; background: var(--bg, #fff); color: var(--text); width: 100%; box-sizing: border-box; }
    small.hint { font-size: 11.5px; color: var(--text-3); line-height: 1.4; }
    small.sample { font-size: 11.5px; color: var(--text-2); font-style: italic; }
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

  /** Existing skill-scope category names (for the missing-category warning). */
  private existingSkillCategories = signal<string[]>([]);
  private skillCategoriesLoaded = false;

  /** Categories referenced by pasted skills rows that don't exist yet — they'll be created on import. */
  protected missingCategories = computed(() => {
    if (this.type() !== 'skills') return [];
    const rows = this.analysis()?.rows ?? [];
    const cats = new Set<string>();
    for (const r of rows) {
      const c = String(r['category'] ?? '').trim();
      if (c) cats.add(c);
    }
    if (cats.size === 0) return [];
    const existing = new Set(this.existingSkillCategories().map(c => c.trim().toLowerCase()));
    return [...cats].filter(c => !existing.has(c.toLowerCase())).sort((a, b) => a.localeCompare(b));
  });

  /** Whether the new-categories mapping popup is shown (skills import only). */
  showCategoryMap = signal(false);
  readonly uncatKey = UNCATEGORIZED_KEY;
  /** Private category plan (category name → disposition). */
  private categoryPlan = signal<Record<string, CategoryPlanEntry> | null>(null);

  /** Rows for the mapping popup: name + current mode (snapshot of missingCategories at open). */
  protected categoryPlanList = computed(() => {
    const plan = this.categoryPlan();
    if (!plan) return [];
    return Object.entries(plan)
      .map(([name, entry]) => ({ name, ...entry }))
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  /** Existing root category names for the "move into" options. */
  existingSkillNames(): string[] {
    return [...this.existingSkillCategories()].sort((a, b) => a.localeCompare(b));
  }

  /** Select value for a plan row ('' = create, UNCATEGORIZED_KEY = keep uncategorized, else target). */
  planModeValue(entry: { mode: CategoryPlanMode; targetName?: string }): string {
    if (entry.mode === 'existing') return entry.targetName ?? '';
    return entry.mode === 'uncategorized' ? UNCATEGORIZED_KEY : '';
  }

  planModeHint(entry: { mode: CategoryPlanMode; targetName?: string }): string {
    if (entry.mode === 'existing') return `Skills will move into “${entry.targetName ?? '…'}”.`;
    if (entry.mode === 'uncategorized') return "Skills won't be in any category.";
    return 'A new container with this name will be created.';
  }

  closeCategoryMap(): void {
    this.categoryPlan.set(null);
    this.showCategoryMap.set(false);
  }

  openCategoryMap(): void {
    const needed = this.missingCategories();
    if (needed.length === 0) return;
    this.categoryPlan.set(
      Object.fromEntries(needed.map(name => [name, { mode: 'create' as CategoryPlanMode }])),
    );
    this.showCategoryMap.set(true);
    void this.ensureSkillCategories();
  }

  setPlanMode(name: string, value: string): void {
    const plan = this.categoryPlan();
    if (!plan) return;
    const next = { ...plan };
    if (value === UNCATEGORIZED_KEY) next[name] = { mode: 'uncategorized' };
    else if (value === '') next[name] = { mode: 'create' };
    else next[name] = { mode: 'existing', targetName: value };
    this.categoryPlan.set(next);
  }

  applyPlan(): void {
    const plan = this.categoryPlan();
    if (!plan) return;
    const rows = this.analysis()?.rows ?? [];
    const prepared = rows.map(r => {
      const c = String(r['category'] ?? '').trim();
      const entry = plan[c];
      if (!entry) return r;
      if (entry.mode === 'uncategorized') return { ...r, category: '' };
      if (entry.mode === 'existing' && entry.targetName) return { ...r, category: entry.targetName };
      return r;
    });
    this.closeCategoryMap();
    void this.performImport(prepared);
  }

  /** Headers detected on the first pasted line, in order. */
  sourceHeaders = signal<string[]>([]);
  /** Target field → exact source header string. */
  mapping = signal<Record<string, string>>({});
  /** Cached table (headers + data rows) parsed from raw(). */
  private tableCache = signal<{ headers: string[]; data: string[][] } | null>(null);

  /** Pure derived state — no writes during render. */
  protected analysis = computed(() => {
    if (!this.raw().trim()) return { rows: [], mappedColumns: [], requiredUnmapped: catalogFor(this.type()).filter(d => d.required) };
    return buildRows(this.raw(), this.type(), this.mapping());
  });

  protected requiredUnmapped = computed(() => this.analysis().requiredUnmapped);

  protected fieldCatalog = computed(() => catalogFor(this.type()));

  fieldMapping(field: string): string {
    return this.mapping()[field] ?? '';
  }

  sampleOf(header: string): string {
    return sampleValue(this.tableCache(), header);
  }

  canImport(): boolean {
    const a = this.analysis();
    return (a.rows.length ?? 0) > 0 && a.requiredUnmapped.length === 0;
  }

  importLabel(): string {
    const n = this.analysis()?.rows.length ?? 0;
    return n > 0 ? `Import ${n.toLocaleString()} row${n === 1 ? '' : 's'}` : 'Import';
  }

  fileLabel(): string {
    return this.fileName() || 'Choose file (CSV, XLSX…)';
  }

  private detect(): void {
    const text = this.raw();
    const table = parseTable(text);
    this.tableCache.set(table);
    const headers = table ? table.headers : [];
    this.sourceHeaders.set(headers);

    const next: Record<string, string> = {};
    const catalog = catalogFor(this.type());
    const prev = this.mapping();
    for (const d of catalog) {
      const h = prev[d.field];
      if (h && headers.includes(h)) next[d.field] = h;
    }
    this.mapping.set(next);
    this.autoMap();
    if (this.type() === 'skills') void this.ensureSkillCategories();
  }

  private async ensureSkillCategories(): Promise<void> {
    if (this.skillCategoriesLoaded) return;
    this.skillCategoriesLoaded = true;
    try {
      const res = await this.http.get<ApiResponse<{ name: string }[]>>('/api/categories/tree?scope=skills');
      this.existingSkillCategories.set((res.data ?? []).map(n => n.name).filter(Boolean));
    } catch {
      // Non-fatal: the warning just won't appear.
      this.skillCategoriesLoaded = false;
    }
  }

  /** Fills unmatched fields from header names/aliases; never overrides explicit choices. */
  autoMap(): void {
    const cat = catalogFor(this.type());
    const headers = this.sourceHeaders();
    if (!headers.length || cat.length === 0) return;
    const m = { ...this.mapping() };
    const used = new Set(Object.values(m).filter(Boolean));
    for (const d of cat) {
      if (m[d.field]) continue;
      const best = pickBestHeader(d, headers, used);
      if (best) { m[d.field] = best; used.add(best); }
    }
    this.mapping.set(m);
  }

  clearMapping(): void {
    this.mapping.set({});
  }

  assignMapping(field: string, header: string): void {
    const m = { ...this.mapping() };
    // A source column can feed exactly one target.
    for (const [f, h] of Object.entries(m)) {
      if (h === header && f !== field) delete m[f];
    }
    if (header) m[field] = header; else delete m[field];
    this.mapping.set(m);
  }

  refreshOnEdit(value: string): void {
    if (this.showCategoryMap()) this.closeCategoryMap();
    this.raw.set(value);
    this.detect();
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
      this.detect();
    } catch {
      this.busy.set(false);
      this.toast.error('Could not read that file');
    }
  }

  clearFile() {
    this.fileName.set('');
    this.raw.set('');
    this.detect();
  }

  resetForAnother() {
    this.closeCategoryMap();
    this.raw.set('');
    this.fileName.set('');
    this.result.set(null);
    this.mapping.set({});
    this.sourceHeaders.set([]);
    this.tableCache.set(null);
  }

  close() {
    this.open.set(false);
    this.resetForAnother();
  }

  doImport() {
    const rows = this.analysis()?.rows ?? [];
    if (rows.length === 0) return;
    if (this.type() === 'skills' && this.missingCategories().length > 0 && !this.showCategoryMap()) {
      this.openCategoryMap();
      return;
    }
    void this.performImport(rows);
  }

  private async performImport(rows: Record<string, unknown>[]) {
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