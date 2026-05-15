import { Component, inject, OnInit, ChangeDetectorRef } from '@angular/core';
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
  private cdr = inject(ChangeDetectorRef);

  entity: EntityType = 'projects';
  id = '';
  item: any;
  fields: any[] = [];
  isLoading = true;
  error: string | null = null;
  showDeleteModal = false;

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') as EntityType;
      this.id = params.get('id')!;
      this.fields = ENTITY_FIELDS[this.entity] || [];
      this.isLoading = true;
      this.error = null;
      this.loadData();
    });
  }

  loadData() {
    const baseUrl = environment.apiUrl || 'http://localhost:8080/api/user-content';
    const url = `${baseUrl}/${this.entity}/${this.id}?t=${new Date().getTime()}`;
    
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
    this.router.navigate(['/my-cv', this.entity, this.id, 'edit']);
  }

  deleteItem() {
    this.showDeleteModal = true;
  }

  confirmDelete() {
    const baseUrl = environment.apiUrl || 'http://localhost:8080/api/user-content';
    const url = `${baseUrl}/${this.entity}/${this.id}`;
    this.http.delete(url, { withCredentials: true }).subscribe({
      next: () => {
        this.router.navigate(['/my-cv', this.entity]);
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
    this.router.navigate(['/my-cv', this.entity]);
  }
}