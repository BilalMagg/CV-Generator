import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { SearchService, SearchResult, SearchStatus } from '@app/services/search.service';
import { extractError } from '@app/shared/error-utils';

type SearchInputMethod = 'text' | 'keywords' | 'entity';

const ENTITY_TYPES = [
  'User', 'CVProfile', 'Experience', 'Project', 'Skill',
  'Education', 'Certification', 'Language', 'Interest',
  'Hackathon', 'AcademicActivity', 'SocialLink',
  'Company', 'Contact', 'Application',
];

@Component({
  selector: 'app-search-agent-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './search-agent-workspace.component.html',
  styleUrl: './search-agent-workspace.component.scss'
})
export class SearchAgentWorkspaceComponent implements OnInit {
  private readonly searchService = inject(SearchService);
  private readonly router = inject(Router);

  activeMethod: SearchInputMethod = 'text';
  textInput = '';
  keywordsInput = '';
  selectedEntityTypes: string[] = [];

  loading = signal(false);
  syncing = signal(false);
  error = signal('');
  results = signal<SearchResult[]>([]);
  hasSearched = signal(false);

  status = signal<SearchStatus>({ synced: false, chunkCount: 0, sourceTypeCounts: {} });
  entityTypes = ENTITY_TYPES;

  ngOnInit(): void {
    this.loadStatus();
  }

  private async loadStatus(): Promise<void> {
    try {
      this.status.set(await this.searchService.getStatus());
    } catch {
      this.status.set({ synced: false, chunkCount: 0, sourceTypeCounts: {} });
    }
  }

  get canSubmit(): boolean {
    switch (this.activeMethod) {
      case 'text': return this.textInput.trim().length > 0;
      case 'keywords': return this.keywordsInput.trim().length > 0;
      case 'entity': return this.textInput.trim().length > 0 && this.selectedEntityTypes.length > 0;
    }
  }

  toggleEntityType(type: string): void {
    const idx = this.selectedEntityTypes.indexOf(type);
    if (idx >= 0) {
      this.selectedEntityTypes.splice(idx, 1);
    } else {
      this.selectedEntityTypes.push(type);
    }
  }

  isTypeSelected(type: string): boolean {
    return this.selectedEntityTypes.includes(type);
  }

  async onSubmit(): Promise<void> {
    if (!this.canSubmit || this.loading()) return;
    this.loading.set(true);
    this.error.set('');
    this.results.set([]);

    try {
      const query = this.activeMethod === 'keywords' ? this.keywordsInput : this.textInput;
      const sourceTypes = this.activeMethod === 'entity'
        ? this.selectedEntityTypes
        : [...ENTITY_TYPES];

      const res = await this.searchService.search(query, sourceTypes, 20);
      this.results.set(res);
      this.hasSearched.set(true);
    } catch (err) {
      this.error.set(extractError(err, 'Search failed'));
    } finally {
      this.loading.set(false);
    }
  }

  async onSync(): Promise<void> {
    this.syncing.set(true);
    try {
      await this.searchService.sync();
      await this.loadStatus();
    } catch (err) {
      this.error.set(extractError(err, 'Sync failed'));
    } finally {
      this.syncing.set(false);
    }
  }

  private percent(pct: number): string {
    return Math.max(0, Math.min(100, Math.round(pct))) + '%';
  }

  scoreColor(score: number): string {
    if (score == null || Number.isNaN(score)) return 'neutral';
    if (score >= 0.5) return 'high';
    if (score >= 0.3) return 'mid';
    return 'low';
  }

  scoreWidth(score: number): string {
    return this.percent((score ?? 0) * 100);
  }

  formatScore(score: number): string {
    return this.percent((score ?? 0) * 100);
  }

  isNa(value: string): boolean {
    const v = value.trim().toUpperCase();
    return v === '' || v === 'N/A';
  }

  resultUrl(r: SearchResult): string {
    const id = r.sourceId;
    switch (r.sourceType) {
      case 'Application': return `/applications/${id}`;
      case 'Company': return `/companies/${id}`;
      case 'Contact': return '/mailbox?tab=contacts';
      case 'User': return '/profile';
      case 'CVProfile': return `/my-career/cvprofiles/${id}`;
      case 'Experience': return `/my-career/experiences/${id}`;
      case 'Project': return `/my-career/projects/${id}`;
      case 'Skill': return `/my-career/skills/${id}`;
      case 'Education': return `/my-career/educations/${id}`;
      case 'Certification': return `/my-career/certifications/${id}`;
      case 'Language': return `/my-career/languages/${id}`;
      case 'Interest': return `/my-career/interests/${id}`;
      case 'Hackathon': return `/my-career/hackathons/${id}`;
      case 'AcademicActivity': return `/my-career/academicactivities/${id}`;
      case 'SocialLink': return `/my-career/sociallinks/${id}`;
      default: return '';
    }
  }

  openResult(r: SearchResult): void {
    const url = this.resultUrl(r);
    if (url) this.router.navigateByUrl(url);
  }

  typeColor(type: string): string {
    const colors: Record<string, string> = {
      Experience: '#6366f1', Project: '#8b5cf6', Skill: '#06b6d4',
      Education: '#f59e0b', Certification: '#10b981', Language: '#ec4899',
      Interest: '#f97316', Hackathon: '#ef4444', AcademicActivity: '#3b82f6',
      SocialLink: '#64748b', Company: '#14b8a6', Contact: '#a855f7',
      Application: '#22c55e', User: '#6366f1', CVProfile: '#8b5cf6',
    };
    return colors[type] ?? '#64748b';
  }

  parseResult(r: SearchResult): { title: string; facts: { label: string; value: string }[] } {
    const parts = r.content.split('.').map(p => p.trim()).filter(Boolean);
    const facts: { label: string; value: string }[] = [];
    let title = r.content;

    parts.forEach((part, i) => {
      const colon = part.indexOf(':');
      const label = colon > -1 ? part.slice(0, colon).trim() : '';
      const value = colon > -1 ? part.slice(colon + 1).trim() : part;

      if (i === 0 && label) {
        title = value || part;
      } else if (i > 0 && (label || value)) {
        facts.push({ label: label || 'Info', value });
      }
    });

    return { title, facts };
  }
}
