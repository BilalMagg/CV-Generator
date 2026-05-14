import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../environments/environment';
import { ENTITY_FIELDS, EntityType } from '../../models/user-content.models';

@Component({
  selector: 'app-entity-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './entity-form.component.html',
  styleUrl: './entity-form.component.css',
})
export class EntityFormComponent implements OnInit {
  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  entity: EntityType = 'projects';
  id: string | null = null;
  form: any = {};
  fields: any[] = [];

  ngOnInit() {
    this.route.paramMap.subscribe(params => {
      this.entity = params.get('entity') as EntityType;
      this.id = params.get('id');
      this.fields = ENTITY_FIELDS[this.entity] || [];
      
      if (this.id) {
        this.loadData();
      } else {
        this.initializeForm();
      }
    });
  }

  initializeForm() {
    this.form = {};
    this.fields.forEach(field => {
      if (field.type === 'checkbox') {
        this.form[field.name] = false;
      } else if (field.type === 'select' && field.options) {
        this.form[field.name] = field.options[0];
      } else {
        this.form[field.name] = '';
      }
    });
  }

  loadData() {
    const url = `${environment.apiUrl}/${this.entity}/${this.id}`;
    this.http.get(url).subscribe(data => {
      this.form = data;
    });
  }

  submit() {
    const baseUrl = environment.apiUrl || 'http://localhost:8083/api';
    if (this.id) {
      const url = `${baseUrl}/${this.entity}/${this.id}`;
      this.http.put(url, this.form).subscribe({
        next: () => {
          this.router.navigate(['/my-cv', this.entity]);
        },
        error: (err) => console.error('Update failed', err)
      });
    } else {
      const url = `${baseUrl}/${this.entity}`;
      this.http.post(url, this.form).subscribe({
        next: () => {
          this.router.navigate(['/my-cv', this.entity]);
        },
        error: (err) => console.error('Create failed', err)
      });
    }
  }

  cancel() {
    if (this.id) {
      this.router.navigate(['/my-cv', this.entity, this.id]);
    } else {
      this.router.navigate(['/my-cv', this.entity]);
    }
  }
}