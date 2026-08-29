import { Component, inject, OnInit, ChangeDetectorRef, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '@env/environment';
import { EntityCardComponent } from '@app/shared/components/entity-card/entity-card.component';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';
import { CategoryTreeComponent } from '@app/shared/components/category-tree/category-tree.component';
import { CategoryService } from '@app/services/category.service';

@Component({
  selector: 'app-entity-list',
  standalone: true,
  imports: [CommonModule, RouterModule, EntityCardComponent, RefreshButtonComponent, CategoryTreeComponent],
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.css',
})
export class EntityListComponent implements OnInit {
    private route:ActivatedRoute = inject(ActivatedRoute);
    private http = inject(HttpClient);
    private router = inject(Router);
    private cdr = inject(ChangeDetectorRef);
    private categoryService = inject(CategoryService);

    entity= '';
    data: any[]=[];
    refreshing = signal(false);
    selectedCategoryIds = signal<string[]>([]);
    matchedIds = signal<Set<string> | null>(null);
    
  goToDetail(id: string){
    this.router.navigate(['/my-career', this.entity, id]);
  }

  displayData(): any[] {
    const matched = this.matchedIds();
    if (!matched) return this.data;
    return this.data.filter((d) => matched.has(d.id));
  }

  onCategoryChange(ids: string[]): void {
    this.selectedCategoryIds.set(ids);
    if (!ids || ids.length === 0) {
      this.matchedIds.set(null);
      return;
    }
    this.categoryService
      .search({ nodeIds: ids, sourceTypes: [this.entity.toLowerCase()] })
      .then((results) => {
        this.matchedIds.set(new Set(results.map((r) => r.sourceId)));
      })
      .catch(() => {
        this.matchedIds.set(null);
      });
  }


    ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') || '';
      this.loadData();
    });
  }

  loadData(){
    const url = `${environment.apiUrl}/api/user-content/${this.entity}?t=${new Date().getTime()}`;
    
    this.http.get<any>(url, { withCredentials: true }).subscribe({
      next: (response) => {
        this.data = response.data || [];
        this.refreshing.set(false);
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error(`[EntityList] Error fetching ${this.entity}:`, err);
        this.refreshing.set(false);
        this.cdr.detectChanges();
      }
    });
  }

  onRefresh() {
    this.refreshing.set(true);
    this.loadData();
  }

  getCardData(item: any) {
    const e = this.entity.toLowerCase();
    switch(e) {
      case 'cvprofiles':
        return { title: item.title, subtitle: item.summary, typeLabel: 'Profile', meta: '', footer: '' };
      case 'projects':
        return { title: item.title, subtitle: item.role, typeLabel: 'Project', meta: item.status, footer: this.formatDate(item.startDate), isCompleted: item.status === 'Completed' };
      case 'skills':
        return { title: item.name, subtitle: item.category, typeLabel: 'Skill', meta: item.level, footer: item.yearsOfExperience ? `${item.yearsOfExperience} years` : '' };
      case 'experiences':
        return { title: item.company, subtitle: item.position, typeLabel: 'Experience', meta: item.location, footer: this.formatDate(item.startDate), isCompleted: item.status === 'Completed' };
      case 'educations':
        return { title: item.institutionName || item.institution, subtitle: item.degreeType || item.degree, typeLabel: 'Education', meta: item.fieldOfStudy, footer: this.formatDate(item.startDate), isCompleted: item.status === 'Completed' };
      case 'certifications':
        return { title: item.name, subtitle: item.issuingOrganization, typeLabel: 'Certification', meta: '', footer: this.formatDate(item.issueDate) };
      case 'languages':
        return { title: item.name, subtitle: item.level, typeLabel: 'Language' };
      case 'interests':
        return { title: item.name, typeLabel: 'Interest' };
      case 'sociallinks':
        return { title: item.platform, subtitle: item.url, typeLabel: 'Social Link' };
      case 'academicactivities':
        return { title: item.title, subtitle: item.organization, typeLabel: 'Academic', footer: this.formatDate(item.startDate) };
      case 'hackathons':
        return { title: item.name, subtitle: item.role, typeLabel: 'Hackathon', meta: item.organization, footer: this.formatDate(item.date) };
      default:
        return { title: item.name || item.title, typeLabel: this.entity };
    }
  }

  formatDate(dateStr: string) {
    if (!dateStr) return '';
    try {
      const date = new Date(dateStr);
      return date.toLocaleDateString();
    } catch {
      return dateStr;
    }
  }
}
