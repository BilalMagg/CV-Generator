import { Component, inject, OnInit } from '@angular/core';
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
    const url=`${environment.apiUrl}/${this.entity}`;
    this.http.get(url).subscribe(data=>{
      this.data = data as any[];
    })
  }

  getCardData(item: any) {
    const e = this.entity.toLowerCase();
    switch(e) {
      case 'cvprofile':
        return { title: item.fullName, subtitle: item.title, typeLabel: 'Profile', meta: item.location, footer: item.email };
      case 'projects':
        return { title: item.title, subtitle: item.role, typeLabel: 'Project', meta: item.status, footer: this.formatDate(item.startDate), isCompleted: item.status === 'Completed' };
      case 'skills':
        return { title: item.name, subtitle: item.category, typeLabel: 'Skill', meta: item.level, footer: item.yearsOfExperience ? `${item.yearsOfExperience} years` : '' };
      case 'experiences':
        return { title: item.company, subtitle: item.position, typeLabel: 'Experience', meta: item.location, footer: this.formatDate(item.startDate), isCompleted: item.isCurrent };
      case 'education':
        return { title: item.institution, subtitle: item.degree, typeLabel: 'Education', meta: item.fieldOfStudy, footer: this.formatDate(item.startDate) };
      case 'certifications':
        return { title: item.name, subtitle: item.issuer, typeLabel: 'Certification', meta: item.credentialId, footer: this.formatDate(item.issueDate) };
      case 'languages':
        return { title: item.name, subtitle: item.proficiency, typeLabel: 'Language' };
      case 'interests':
        return { title: item.name, subtitle: item.category, typeLabel: 'Interest' };
      case 'sociallinks':
        return { title: item.platform, subtitle: item.url, typeLabel: 'Social Link' };
      case 'academicactivities':
        return { title: item.title, subtitle: item.organization, typeLabel: 'Academic', footer: this.formatDate(item.startDate) };
      case 'hackathons':
        return { title: item.name, subtitle: item.role, typeLabel: 'Hackathon', meta: item.location, footer: this.formatDate(item.date) };
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
