import { Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Component({
  selector: 'app-entity-detail',
  templateUrl: './entity-detail.component.html',
  styleUrl: './entity-detail.component.css'
})
export class EntityDetailComponent {

  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private http = inject(HttpClient);

  entity = '';
  id = '';
  item: any;

  ngOnInit() {

    // 1. récupérer params URL
    this.entity = this.route.snapshot.paramMap.get('entity')!;
    this.id = this.route.snapshot.paramMap.get('id')!;

    // 2. appeler backend
    this.loadData();
  }

  loadData() {
    const url = `${environment.apiUrl}/${this.entity}/${this.id}`;

    this.http.get(url).subscribe(res => {
      this.item = res;
    });
  }

  goToEdit() {
    this.router.navigate(['/my-cv', this.entity, this.id, 'edit']);
  }
}