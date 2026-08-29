import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '@env/environment';
import { ENTITY_FIELDS, EntityType } from '@app/models/user-content.models';
import { CategoryService } from '@app/services/category.service';
import { CategoryTreeComponent } from '@app/shared/components/category-tree/category-tree.component';

@Component({
  selector: 'app-entity-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, CategoryTreeComponent],
  templateUrl: './entity-details.component.html',
  styleUrl: './entity-details.component.css'
})
export class EntityDetailsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);

  entity: EntityType = 'projects';
  id = '';
  item: any;
  fields: any[] = [];
  isLoading = true;
  error: string | null = null;
  showDeleteModal = false;

  private readonly TAXONOMY_SCOPES = new Set([
    'projects', 'experiences', 'educations', 'certifications',
    'skills', 'languages', 'hackathons', 'interests', 'academicactivities',
  ]);
  get isTaxonomy(): boolean {
    return this.TAXONOMY_SCOPES.has(this.entity.toLowerCase());
  }
  private get scope(): string {
    return this.entity.toLowerCase();
  }

  // Categorize feature
  showCategorizeModal = false;
  categorizeStep: 'choice' | 'editing' = 'choice';
  treeSelection: string[] = [];
  isSuggesting = false;
  suggestError: string | null = null;
  currentTags: string[] = [];
  currentTagNames: string[] = [];

  private categoryService = inject(CategoryService);

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') as EntityType;
      this.id = params.get('id')!;
      this.fields = ENTITY_FIELDS[this.entity] || [];
      this.isLoading = true;
      this.error = null;
      this.loadData();
      if (this.isTaxonomy) this.loadCurrentTags();
    });
  }

  private async loadCurrentTags(): Promise<void> {
    try {
      const [tags, nameMap] = await Promise.all([
        this.categoryService.getTags(this.scope, this.id),
        this.categoryService.getNodeNameMap(this.scope),
      ]);
      this.currentTags = tags;
      this.currentTagNames = tags.map(id => nameMap.get(id) || id);
    } catch {
      this.currentTags = [];
      this.currentTagNames = [];
    }
    this.cdr.detectChanges();
  }

  openCategorize() {
    this.showCategorizeModal = true;
    this.categorizeStep = 'choice';
    this.suggestError = null;
    this.treeSelection = [];
    this.cdr.detectChanges();
  }

  closeCategorize() {
    this.showCategorizeModal = false;
    this.categorizeStep = 'choice';
    this.suggestError = null;
    this.treeSelection = [];
    this.cdr.detectChanges();
  }

  chooseManual() {
    this.treeSelection = [...this.currentTags];
    this.categorizeStep = 'editing';
    this.cdr.detectChanges();
  }

  async chooseAI() {
    this.isSuggesting = true;
    this.suggestError = null;
    this.cdr.detectChanges();
    try {
      const suggested = await this.categoryService.suggestTags(this.scope, this.id);
      this.treeSelection = Array.from(new Set([...this.currentTags, ...suggested]));
      this.categorizeStep = 'editing';
    } catch {
      this.suggestError = 'Could not generate suggestions. Please try again or categorize manually.';
    } finally {
      this.isSuggesting = false;
      this.cdr.detectChanges();
    }
  }

  onTreeSelectionChange(ids: string[]) {
    this.treeSelection = ids;
  }

  saveCategorize() {
    this.categoryService
      .setTags({ sourceType: this.scope, sourceId: this.id, nodeIds: this.treeSelection })
      .then(() => {
        this.showCategorizeModal = false;
        this.categorizeStep = 'choice';
        this.loadCurrentTags();
      })
      .catch(() => {
        this.suggestError = 'Failed to save categories. Please try again.';
      });
  }

  loadData() {
    const url = `${environment.apiUrl}/api/user-content/${this.entity}/${this.id}?t=${new Date().getTime()}`;
    
    this.http.get(url, { withCredentials: true }).subscribe({
      next: (res: any) => {
        this.item = res.data || res;
        this.isLoading = false;
        this.error = null;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Load failed', err);
        this.isLoading = false;
        this.error = 'Failed to load data';
        this.cdr.detectChanges();
      }
    });
  }

  getFieldValue(fieldName: string) {
    const value = this.item?.[fieldName];
    if (value === undefined || value === null) return '-';
    
    // Format date if needed
    const field = this.fields.find(f => f.name === fieldName);
    if (field?.type === 'date' && value) {
      try {
        return new Date(value).toLocaleDateString();
      } catch {
        return value;
      }
    }
    
    // Format boolean
    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }
    
    return value;
  }

  goToEdit() {
    this.router.navigate(['/my-career', this.entity, this.id, 'edit']);
  }

  deleteItem() {
    this.showDeleteModal = true;
  }

  confirmDelete() {
    const url = `${environment.apiUrl}/api/user-content/${this.entity}/${this.id}`;
    this.http.delete(url, { withCredentials: true }).subscribe({
      next: () => {
        this.router.navigate(['/my-career', this.entity]);
      },
      error: (err) => {
        console.error('Delete failed', err);
        this.showDeleteModal = false;
      }
    });
  }

  cancelDelete() {
    this.showDeleteModal = false;
  }

  goBack() {
    this.router.navigate(['/my-career', this.entity]);
  }
}