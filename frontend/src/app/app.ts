import { Component, inject } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { HeaderComponent } from './layout/header/header.component';
import { SidebarComponent } from './layout/sidebar/sidebar.component';
import { RevealOverlayComponent } from './pages/reveal-overlay/reveal-overlay.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, SidebarComponent, RevealOverlayComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private router = inject(Router);

  isAuthPage = false;
  showHeader = false;

  ngOnInit(): void {
    this.router.events.subscribe(() => {
      const url = this.router.url;
      this.isAuthPage = url === '/' || url === '/login' || url === '/register' || url.startsWith('/about') || url.startsWith('/contact');
      this.showHeader = !this.isAuthPage;
    });
    const initialUrl = this.router.url;
    this.isAuthPage = initialUrl === '/' || initialUrl === '/login' || initialUrl === '/register' || initialUrl.startsWith('/about') || initialUrl.startsWith('/contact');
    this.showHeader = !this.isAuthPage;
  }
}
