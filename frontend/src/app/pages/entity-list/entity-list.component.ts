import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { Router } from '@angular/router'; 

@Component({
  selector: 'app-entity-list',
  imports: [],
  templateUrl: './entity-list.component.html',
  styleUrl: './entity-list.component.css',
})
export class EntityListComponent {
    private route:ActivatedRoute = inject(ActivatedRoute);
    private http = inject(HttpClient);
    private router = inject(Router);


    entity= '';
    data: any[]=[];
    
    goToDetail(id: number){
      this.router.navigate(['/my-cv',this.entity,id]);
    }
    ngOnInit() {
    // 1. récupérer entity depuis URL
    this.entity = this.route.snapshot.paramMap.get('entity') || '';

    // 2. charger les données
    this.loadData();
  }
  loadData(){
    const url=`${environment.apiUrl}/${this.entity}`;
    this.http.get(url).subscribe(data=>{
      this.data = data as any[];
    })
  }
}
