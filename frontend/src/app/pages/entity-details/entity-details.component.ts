import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { environment } from '../../../environments/environment';
import { ENTITY_FIELDS, EntityType } from '../../models/user-content.models';

@Component({
  selector: 'app-entity-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './entity-details.component.html',
  styleUrl: './entity-details.component.css'
})
export class EntityDetailsComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);

  entity: EntityType = 'projects';
  id = '';
  item: any;
  fields: any[] = [];

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') as EntityType;
      this.id = params.get('id')!;
      this.fields = ENTITY_FIELDS[this.entity] || [];
      this.loadData();
    });
  }

  loadData() {
    const url = `${environment.apiUrl}/${this.entity}/${this.id}`;
    this.http.get(url).subscribe({
      next: (res) => {
        this.item = res;
      },
      error: (err) => console.error('Load failed', err)
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
    this.router.navigate(['/my-cv', this.entity, this.id, 'edit']);
  }

  deleteItem() {
    if (confirm(`Are you sure you want to delete this ${this.entity}?`)) {
      const url = `${environment.apiUrl}/${this.entity}/${this.id}`;
      this.http.delete(url).subscribe({
        next: () => {
          this.router.navigate(['/my-cv', this.entity]);
        },
        error: (err) => console.error('Delete failed', err)
      });
    }
  }

  goBack() {
    this.router.navigate(['/my-cv', this.entity]);
  }
}