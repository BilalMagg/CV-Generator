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
        // initialise le formulaire avec des valeurs par défaut pour l'affichage rapide,
        // puis récupère les données réelles en asynchrone et les applique.
        this.initializeForm();
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
    const baseUrl = environment.apiUrl || 'http://localhost:8080/api/user-content';
    const url = `${baseUrl}/${this.entity}/${this.id}`;
    this.http.get<any>(url, { withCredentials: true }).subscribe(response => {
      this.form = response.data || {};
    });
  }

  submit() {
    const baseUrl = environment.apiUrl || 'http://localhost:8080/api/user-content';

    // Clean up the form data before sending to the backend
    const payload: any = {};
    this.fields.forEach(field => {
      const value = this.form[field.name];
      if (field.type === 'date') {
        // Convert empty date strings to null to avoid DateTime.MinValue in C#
        payload[field.name] = value && value !== '' ? new Date(value).toISOString() : null;
      } else if (field.type === 'number') {
        payload[field.name] = value !== '' && value !== null ? Number(value) : null;
      } else if (field.type === 'checkbox') {
        payload[field.name] = Boolean(value);
      } else {
        // Send empty string as null for optional text fields
        payload[field.name] = value !== '' ? value : null;
      }
    });

    if (this.id) {
      const url = `${baseUrl}/${this.entity}/${this.id}`;
      this.http.put(url, payload, { withCredentials: true }).subscribe({
        next: () => {
          this.router.navigate(['/my-cv', this.entity]);
        },
        error: (err) => console.error('Update failed', err)
      });
    } else {
      const url = `${baseUrl}/${this.entity}`;
      this.http.post(url, payload, { withCredentials: true }).subscribe({
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