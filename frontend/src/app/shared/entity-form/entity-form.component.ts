import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-entity-form',
  templateUrl: './entity-form.component.html',
  styleUrl: './entity-form.component.css',
})
export class EntityFormComponent {

  private http = inject(HttpClient);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  entity = '';
  id: string | null = null;

  form: any = {};

  ngOnInit() {

    // 1. récupérer entity + id depuis URL
    this.entity = this.route.snapshot.paramMap.get('entity')!;
    this.id = this.route.snapshot.paramMap.get('id');

    // 2. si EDIT → charger données
    if (this.id) {
      this.loadData();
    }
  }

  // 🔵 GET data pour EDIT
  loadData() {
    const url = `${environment.apiUrl}/${this.entity}/${this.id}`;

    this.http.get(url).subscribe(data => {
      this.form = data; // 👈 remplit le formulaire
    });
  }

  // 🟢 ADD + EDIT
  submit() {

    if (this.id) {
      // EDIT (PUT)
      const url = `${environment.apiUrl}/${this.entity}/${this.id}`;

      this.http.put(url, this.form).subscribe(() => {
        alert('Updated successfully!');
        this.router.navigate(['/my-cv', this.entity]);
      });

    } else {
      // ADD (POST)
      const url = `${environment.apiUrl}/${this.entity}`;

      this.http.post(url, this.form).subscribe(() => {
        alert('Added successfully!');
        this.router.navigate(['/my-cv', this.entity]);
      });
    }
  }
}