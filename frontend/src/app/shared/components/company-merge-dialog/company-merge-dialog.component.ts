import { Component, computed, inject, input, model, OnChanges, output, signal, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  CompanyService,
  CompanyDto,
  CompanyMergePreviewDto,
  CompanyMergeRequestDto,
  MergeContactResolutionDto,
  MergeApplicationResolutionDto,
  CompanyCredentialDto,
  CompanyMergeCandidateDto,
} from '@app/services/company.service';
import { ToastService } from '@app/services/toast.service';

interface FieldChoice {
  field: string;
  label: string;
  valueA: string | number | null | undefined;
  valueB: string | number | null | undefined;
  chosen: 'A' | 'B';
}

@Component({
  selector: 'app-company-merge-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './company-merge-dialog.component.html',
  styleUrl: './company-merge-dialog.component.scss',
})
export class CompanyMergeDialogComponent implements OnChanges {
  private readonly companyService = inject(CompanyService);
  private readonly toast = inject(ToastService);

  open = model(false);
  initialCompanyId = input<string | null>(null);
  merged = output<CompanyDto>();

  // Company selection
  allCompanies = signal<CompanyDto[]>([]);
  loadingCompanies = signal(false);
  companyAId = signal<string>('');
  companyBId = signal<string>('');

  /** Suggested near-duplicate pairs (same base name / prefix variants like "Inetum" vs "Inetum Maroc"). */
  candidates = signal<CompanyMergeCandidateDto[]>([]);

  // Preview state
  previewLoading = signal(false);
  previewError = signal<string | null>(null);
  preview = signal<CompanyMergePreviewDto | null>(null);

  // Surviving company choice: which row ID survives in DB
  // 'A' means Company A survives, 'B' means Company B survives
  survivingChoice = signal<'A' | 'B'>('A');

  // Name choice
  nameOption = signal<'A' | 'B' | 'custom'>('A');
  customName = signal<string>('');

  // Scalar field choices ('A' or 'B')
  fieldChoices = signal<Record<string, 'A' | 'B'>>({
    websiteUrl: 'A',
    location: 'A',
    country: 'A',
    sector: 'A',
    size: 'A',
    specialties: 'A',
    linkedInUrl: 'A',
    foundedYear: 'A',
    description: 'A',
    logo: 'A',
  });

  // Note choice
  noteOption = signal<'A' | 'B' | 'combine' | 'custom'>('combine');
  resultingNote = signal<string>('');

  // Credentials to keep (IDs)
  keptCredentialIds = signal<Set<string>>(new Set());

  // Duplicate contact resolutions (key: pair index)
  contactResolutions = signal<Record<number, 'KeepA' | 'KeepB' | 'KeepBoth'>>({});

  // Duplicate application resolutions (key: pair index)
  appResolutions = signal<Record<number, { mode: 'Merge' | 'KeepBoth'; primary: 'A' | 'B' }>>({});

  // Submission state
  merging = signal(false);
  mergeError = signal<string | null>(null);

  ngOnChanges(changes: SimpleChanges) {
    if (changes['open'] && this.open()) {
      this.resetState();
      this.loadCompanies();
    }
  }

  private resetState() {
    this.preview.set(null);
    this.previewError.set(null);
    this.mergeError.set(null);
    this.companyAId.set(this.initialCompanyId() || '');
    this.companyBId.set('');
    this.survivingChoice.set('A');
    this.nameOption.set('A');
    this.customName.set('');
    this.noteOption.set('combine');
    this.resultingNote.set('');
    this.keptCredentialIds.set(new Set());
    this.contactResolutions.set({});
    this.appResolutions.set({});
  }

  async loadCompanies() {
    this.loadingCompanies.set(true);
    try {
      const res = await this.companyService.getCompanies({ pageSize: 500, sortBy: 'name', sortDir: 'asc' });
      if (res.success && res.data) {
        this.allCompanies.set(res.data.items);
        if (this.initialCompanyId()) {
          this.companyAId.set(this.initialCompanyId()!);
        }
      }
    } catch {
      this.toast.error('Failed to load companies list');
    } finally {
      this.loadingCompanies.set(false);
    }
    try {
      const cand = await this.companyService.getMergeCandidates();
      this.candidates.set(cand.data ?? []);
    } catch {
      /* optional */
    }
  }

  /** Fills A/B from a suggested pair: the OLDER row becomes A (surviving default per design). */
  pickCandidate(cand: CompanyMergeCandidateDto): void {
    const aOlder = new Date(cand.createdA).getTime() <= new Date(cand.createdB).getTime();
    this.companyAId.set(aOlder ? cand.idA : cand.idB);
    this.companyBId.set(aOlder ? cand.idB : cand.idA);
    this.resetChoices();
    void this.loadPreview();
  }

  /** Resets the decision signals after choosing a new pair. */
  private resetChoices(): void {
    this.preview.set(null);
    this.previewError.set(null);
    this.mergeError.set(null);
    this.survivingChoice.set('A');
    this.nameOption.set('A');
    this.customName.set('');
    this.noteOption.set('combine');
    this.resultingNote.set('');
    this.keptCredentialIds.set(new Set());
    this.contactResolutions.set({});
    this.appResolutions.set({});
  }

  onCompanyAChange(id: string) {
    this.companyAId.set(id);
    if (id && this.companyBId() && id !== this.companyBId()) {
      this.loadPreview();
    } else {
      this.preview.set(null);
    }
  }

  onCompanyBChange(id: string) {
    this.companyBId.set(id);
    if (this.companyAId() && id && this.companyAId() !== id) {
      this.loadPreview();
    } else {
      this.preview.set(null);
    }
  }

  swapCompanies() {
    const a = this.companyAId();
    const b = this.companyBId();
    this.companyAId.set(b);
    this.companyBId.set(a);
    if (this.companyAId() && this.companyBId() && this.companyAId() !== this.companyBId()) {
      this.loadPreview();
    }
  }

  async loadPreview() {
    const aId = this.companyAId();
    const bId = this.companyBId();
    if (!aId || !bId || aId === bId) return;

    this.previewLoading.set(true);
    this.previewError.set(null);
    this.mergeError.set(null);

    try {
      const res = await this.companyService.getMergePreview(aId, bId);
      if (res.success && res.data) {
        const data = res.data;
        this.preview.set(data);

        // Initialize name option
        this.nameOption.set('A');
        this.customName.set(data.companyA.name);

        // Determine default surviving company (prefer company with older creation date or A)
        const aCreated = new Date(data.companyA.createdAt).getTime();
        const bCreated = new Date(data.companyB.createdAt).getTime();
        this.survivingChoice.set(aCreated <= bCreated ? 'A' : 'B');

        // Initialize scalar fields
        const defaults: Record<string, 'A' | 'B'> = {
          websiteUrl: data.companyA.websiteUrl ? 'A' : (data.companyB.websiteUrl ? 'B' : 'A'),
          location: data.companyA.location ? 'A' : (data.companyB.location ? 'B' : 'A'),
          country: data.companyA.country ? 'A' : (data.companyB.country ? 'B' : 'A'),
          sector: data.companyA.sector ? 'A' : (data.companyB.sector ? 'B' : 'A'),
          size: data.companyA.size ? 'A' : (data.companyB.size ? 'B' : 'A'),
          specialties: (data.companyA as unknown as { specialties?: string }).specialties ? 'A' : 'B',
          linkedInUrl: data.companyA.linkedInUrl ? 'A' : (data.companyB.linkedInUrl ? 'B' : 'A'),
          foundedYear: data.companyA.foundedYear ? 'A' : (data.companyB.foundedYear ? 'B' : 'A'),
          description: data.companyA.description ? 'A' : (data.companyB.description ? 'B' : 'A'),
          logo: (data.companyA.logoUrl || data.companyA.logoImageId) ? 'A' : 'B',
        };
        this.fieldChoices.set(defaults);

        // Initialize notes
        const noteA = data.companyA.note?.trim() || '';
        const noteB = data.companyB.note?.trim() || '';
        if (noteA && noteB && noteA !== noteB) {
          this.noteOption.set('combine');
          this.resultingNote.set(`${noteA}\n\n---\n\n${noteB}`);
        } else if (noteA) {
          this.noteOption.set('A');
          this.resultingNote.set(noteA);
        } else if (noteB) {
          this.noteOption.set('B');
          this.resultingNote.set(noteB);
        } else {
          this.noteOption.set('custom');
          this.resultingNote.set('');
        }

        // Initialize credentials (keep all by default)
        const credIds = new Set<string>();
        data.credentialsA.forEach(c => credIds.add(c.id));
        data.credentialsB.forEach(c => credIds.add(c.id));
        this.keptCredentialIds.set(credIds);

        // Initialize contact duplicate resolutions
        const contactRes: Record<number, 'KeepA' | 'KeepB' | 'KeepBoth'> = {};
        data.duplicateContacts.forEach((_, idx) => {
          contactRes[idx] = 'KeepA';
        });
        this.contactResolutions.set(contactRes);

        // Initialize application duplicate resolutions
        const appRes: Record<number, { mode: 'Merge' | 'KeepBoth'; primary: 'A' | 'B' }> = {};
        data.duplicateApplications.forEach((_, idx) => {
          appRes[idx] = { mode: 'Merge', primary: 'A' };
        });
        this.appResolutions.set(appRes);
      } else {
        this.previewError.set(res.message || 'Failed to load merge preview');
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error loading preview';
      this.previewError.set(msg);
    } finally {
      this.previewLoading.set(false);
    }
  }

  // Name calculation
  effectiveFinalName = computed<string>(() => {
    const opt = this.nameOption();
    const p = this.preview();
    if (!p) return '';
    if (opt === 'A') return p.companyA.name;
    if (opt === 'B') return p.companyB.name;
    return this.customName().trim();
  });

  setNameOption(opt: 'A' | 'B' | 'custom') {
    this.nameOption.set(opt);
    const p = this.preview();
    if (!p) return;
    if (opt === 'A') this.customName.set(p.companyA.name);
    else if (opt === 'B') this.customName.set(p.companyB.name);
  }

  // Preset buttons for field selection
  useAllFrom(which: 'A' | 'B') {
    const cur = { ...this.fieldChoices() };
    for (const k of Object.keys(cur)) {
      cur[k] = which;
    }
    this.fieldChoices.set(cur);
    this.setNoteOption(which);
  }

  setFieldChoice(field: string, which: 'A' | 'B') {
    this.fieldChoices.update(prev => ({ ...prev, [field]: which }));
  }

  setNoteOption(opt: 'A' | 'B' | 'combine' | 'custom') {
    this.noteOption.set(opt);
    const p = this.preview();
    if (!p) return;
    const noteA = p.companyA.note?.trim() || '';
    const noteB = p.companyB.note?.trim() || '';

    if (opt === 'A') this.resultingNote.set(noteA);
    else if (opt === 'B') this.resultingNote.set(noteB);
    else if (opt === 'combine') {
      if (noteA && noteB) this.resultingNote.set(`${noteA}\n\n---\n\n${noteB}`);
      else this.resultingNote.set(noteA || noteB);
    }
  }

  toggleCredential(id: string) {
    this.keptCredentialIds.update(set => {
      const next = new Set(set);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  selectAllCredentials(select: boolean) {
    const p = this.preview();
    if (!p) return;
    if (!select) {
      this.keptCredentialIds.set(new Set());
    } else {
      const next = new Set<string>();
      p.credentialsA.forEach(c => next.add(c.id));
      p.credentialsB.forEach(c => next.add(c.id));
      this.keptCredentialIds.set(next);
    }
  }

  setContactResolution(index: number, mode: 'KeepA' | 'KeepB' | 'KeepBoth') {
    this.contactResolutions.update(prev => ({ ...prev, [index]: mode }));
  }

  setAppResolutionMode(index: number, mode: 'Merge' | 'KeepBoth') {
    this.appResolutions.update(prev => ({
      ...prev,
      [index]: { ...prev[index], mode },
    }));
  }

  setAppResolutionPrimary(index: number, primary: 'A' | 'B') {
    this.appResolutions.update(prev => ({
      ...prev,
      [index]: { ...prev[index], primary },
    }));
  }

  // Submission
  async submitMerge() {
    const p = this.preview();
    const finalName = this.effectiveFinalName();
    if (!p || !finalName) return;

    this.merging.set(true);
    this.mergeError.set(null);

    const isASurviving = this.survivingChoice() === 'A';
    const target = isASurviving ? p.companyA : p.companyB;
    const source = isASurviving ? p.companyB : p.companyA;

    const fields = this.fieldChoices();

    // Map fields
    const websiteUrl = fields['websiteUrl'] === 'A' ? p.companyA.websiteUrl : p.companyB.websiteUrl;
    const location = fields['location'] === 'A' ? p.companyA.location : p.companyB.location;
    const country = fields['country'] === 'A' ? p.companyA.country : p.companyB.country;
    const sector = fields['sector'] === 'A' ? p.companyA.sector : p.companyB.sector;
    const size = fields['size'] === 'A' ? p.companyA.size : p.companyB.size;
    const specialties = fields['specialties'] === 'A'
      ? (p.companyA as unknown as { specialties?: string }).specialties
      : (p.companyB as unknown as { specialties?: string }).specialties;
    const linkedInUrl = fields['linkedInUrl'] === 'A' ? p.companyA.linkedInUrl : p.companyB.linkedInUrl;
    const foundedYear = fields['foundedYear'] === 'A' ? p.companyA.foundedYear : p.companyB.foundedYear;
    const description = fields['description'] === 'A' ? p.companyA.description : p.companyB.description;
    const logoUrl = fields['logo'] === 'A' ? p.companyA.logoUrl : p.companyB.logoUrl;
    const logoImageId = fields['logo'] === 'A' ? p.companyA.logoImageId : p.companyB.logoImageId;

    // Contact resolutions DTO
    const contactResList: MergeContactResolutionDto[] = [];
    p.duplicateContacts.forEach((pair, idx) => {
      const mode = this.contactResolutions()[idx] || 'KeepBoth';
      contactResList.push({
        mode,
        contactAId: pair.contactA.id,
        contactBId: pair.contactB.id,
      });
    });

    // App resolutions DTO
    const appResList: MergeApplicationResolutionDto[] = [];
    p.duplicateApplications.forEach((pair, idx) => {
      const res = this.appResolutions()[idx] || { mode: 'Merge', primary: 'A' };
      const primaryId = res.primary === 'A' ? pair.applicationA.id : pair.applicationB.id;
      const secondaryId = res.primary === 'A' ? pair.applicationB.id : pair.applicationA.id;
      appResList.push({
        mode: res.mode,
        primaryApplicationId: primaryId,
        secondaryApplicationId: secondaryId,
      });
    });

    const payload: CompanyMergeRequestDto = {
      sourceCompanyId: source.id,
      targetCompanyId: target.id,
      finalName,
      websiteUrl,
      location,
      country,
      sector,
      size,
      specialties,
      linkedInUrl,
      foundedYear,
      description,
      logoUrl,
      logoImageId,
      note: this.resultingNote().trim() || null,
      keepCredentialIds: Array.from(this.keptCredentialIds()),
      contactResolutions: contactResList,
      applicationResolutions: appResList,
    };

    try {
      const res = await this.companyService.mergeCompanies(payload);
      if (res.success && res.data) {
        this.toast.success(`Successfully merged companies into "${res.data.name}"`);
        this.merged.emit(res.data);
        this.close();
      } else {
        this.mergeError.set(res.message || 'Merge failed');
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error during merge execution';
      this.mergeError.set(msg);
    } finally {
      this.merging.set(false);
    }
  }

  close() {
    this.open.set(false);
  }
}
