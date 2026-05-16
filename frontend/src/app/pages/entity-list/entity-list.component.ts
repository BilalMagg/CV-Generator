import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';
import { EntityCardComponent } from '../../shared/components/entity-card/entity-card.component';

@Component({
  selector: 'app-entity-list',
  standalone: true,
  imports: [CommonModule, RouterModule, EntityCardComponent],
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.css',
})
export class EntityListComponent implements OnInit {
    private route:ActivatedRoute = inject(ActivatedRoute);
    private http = inject(HttpClient);
    private router = inject(Router);
    private cdr = inject(ChangeDetectorRef);

    entity= '';
    data: any[]=[];
    
    goToDetail(id: string){
      this.router.navigate(['/my-cv', this.entity, id]);
    }

    ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') || '';
      this.loadData();
    });
  }

  loadData(){
    const baseUrl = environment.apiUrl || 'http://localhost:8080/api/user-content';
    const url = `${baseUrl}/${this.entity}?t=${new Date().getTime()}`;
    
    this.http.get<any>(url, { withCredentials: true }).subscribe({
      next: (response) => {
        this.data = response.data || [];
        this.cdr.detectChanges(); // Force UI update!
      },
      error: (err) => {
        console.error(`[EntityList] Error fetching ${this.entity}:`, err);
      }
    });
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
