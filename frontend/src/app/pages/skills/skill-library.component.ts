import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { environment } from '@env/environment';
import { TaxonomyNode } from '@app/models/category.model';
import { CategoryService } from '@app/services/category.service';
import { ToastService } from '@app/services/toast.service';

interface SkillRow {
  id: string;
  name: string;
  level: string;
  category: string;
}

const LEVEL_DOTS: Record<string, number> = {
  Beginner: 1,
  Intermediate: 2,
  Advanced: 3,
  Expert: 4,
};

const DOT_INDEXES = [0, 1, 2, 3, 4];

@Component({
  selector: 'app-skill-library',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './skill-library.component.html',
  styleUrl: './skill-library.component.scss',
})
export class SkillLibraryComponent implements OnInit {
  private http = inject(HttpClient);
  private router = inject(Router);
  private categoryService = inject(CategoryService);
  private toast = inject(ToastService);

  categories = signal<TaxonomyNode[]>([]);
  skills = signal<SkillRow[]>([]);
  collapsed = signal<Set<string>>(new Set());
  search = signal('');
  loading = signal(false);
  newCategory = '';
  dots = DOT_INDEXES;

  /** sourceId -> node ids (tag grouping for the whole skills scope). */
  private tagBySkill = new Map<string, string[]>();

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    if (!q) return this.skills();
    return this.skills().filter(s => s.name.toLowerCase().includes(q));
  });

  readonly containers = computed(() => {
    const byId = new Map<string, string>();
    this.categories().forEach(c => byId.set(c.id, c.name));
    const byNameId = new Map<string, string>();
    this.categories().forEach(c => byNameId.set(c.name.trim().toLowerCase(), c.id));

    const groups: { id: string; name: string; skills: SkillRow[] }[] = [];
    this.categories().forEach(c => groups.push({ id: c.id, name: c.name, skills: [] }));
    const bucketById = new Map<string, SkillRow[]>();
    groups.forEach(g => bucketById.set(g.id, g.skills));
    const uncategorized: SkillRow[] = [];

    for (const s of this.filtered()) {
      const nodeIds = this.tagBySkill.get(s.id) ?? [];
      let hit = nodeIds.find(id => byId.has(id));
      if (!hit && s.category) {
        const byName = byNameId.get(s.category.trim().toLowerCase());
        if (byName) hit = byName;
      }
      if (hit) {
        bucketById.get(hit)?.push(s);
      } else {
        uncategorized.push(s);
      }
    }

    groups.forEach(g => g.skills.sort((a, b) => a.name.localeCompare(b.name)));
    uncategorized.sort((a, b) => a.name.localeCompare(b.name));

    if (uncategorized.length) {
      groups.push({ id: 'uncategorized', name: 'Uncategorized', skills: uncategorized });
    }
    return groups;
  });

  readonly totalSkills = computed(() => this.skills().length);

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.loading.set(true);
    this.loadCategoriesAndSkills();
  }

  private async loadCategoriesAndSkills(): Promise<void> {
    try {
      const [tree, skillRes, tags] = await Promise.all([
        this.categoryService.getTree('skills'),
        firstValueFrom(this.http.get<any>(`${environment.apiUrl}/api/user-content/skills`, { withCredentials: true })),
        this.categoryService.getTagsForScope('skills'),
      ]);

      const raw = skillRes?.data ?? skillRes ?? [];
      const rows: SkillRow[] = (raw || []).map((s: any) => ({
        id: String(s.id ?? s.Id),
        name: s.name ?? s.Name ?? '',
        level: (s.level ?? s.Level ?? '').trim(),
        category: (s.category ?? s.Category ?? '').trim(),
      }));

      this.tagBySkill = new Map();
      (tags || []).forEach(t => {
        if (t.sourceId) this.tagBySkill.set(String(t.sourceId), (t.nodeIds || []).map(String));
      });

      this.categories.set(tree || []);
      this.skills.set(rows.filter(r => r.id.length > 0));
    } catch (err) {
      console.error('Failed to load skills', err);
      this.toast.error('Could not load skills.');
    } finally {
      this.loading.set(false);
    }
  }

  isCollapsed(id: string): boolean {
    return this.collapsed().has(id);
  }

  toggleCollapsed(id: string): void {
    const next = new Set(this.collapsed());
    if (next.has(id)) next.delete(id);
    else next.add(id);
    this.collapsed.set(next);
  }

  dotFill(level: string, i: number): boolean {
    const filled = LEVEL_DOTS[level] ?? 0;
    return i < filled;
  }

  addSkill(category?: string): void {
    this.router.navigate(['/my-career', 'skills', 'add'], category ? { queryParams: { category } } : undefined);
  }

  openSkill(id: string): void {
    this.router.navigate(['/my-career', 'skills', id]);
  }

  createCategory(): void {
    const name = this.newCategory.trim();
    if (!name) return;
    this.categoryService
      .createNode('skills', name)
      .then(() => {
        this.newCategory = '';
        this.loadCategoriesAndSkills();
      })
      .catch(err => {
        const msg = this.errorText(err);
        this.toast.error(msg || 'Could not create category.');
      });
  }

  deleteCategory(id: string, name: string): void {
    if (!confirm(`Delete category "${name}"?`)) return;
    this.categoryService
      .deleteNode(id)
      .then(() => this.loadCategoriesAndSkills())
      .catch(err => {
        const msg = this.errorText(err);
        this.toast.error(msg || 'Could not delete category.');
      });
  }

  private errorText(err: any): string {
    const e = err?.error;
    if (typeof e === 'string') return e;
    if (e?.message) return String(e.message);
    if (e?.errors) return String(e.errors);
    return err?.message ? String(err.message) : '';
  }
}