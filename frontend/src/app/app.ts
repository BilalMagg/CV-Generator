import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, SidebarComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private router = inject(Router);

  isAuthPage = false;

  ngOnInit(): void {
    this.router.events.subscribe(() => {
      const url = this.router.url;
      this.isAuthPage = url === '/' || url === '/login' || url === '/register';
    });
    this.isAuthPage = this.router.url === '/' || this.router.url === '/login' || this.router.url === '/register';
  }
}
