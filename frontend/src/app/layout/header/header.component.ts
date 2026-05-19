import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  private authService = inject(AuthService);
  private router = inject(Router);

  pageName = 'Dashboard';

  private readonly ROUTE_NAMES: Record<string, string> = {
    '/applications/dashboard': 'Dashboard',
    '/applications/kanban':    'Applications',
    '/applications/list':      'Applications',
    '/applications/analytics': 'Analytics',
    '/applications/calendar':  'Applications',
    '/my-cv':                  'My CV',
    '/settings':               'Settings',
  };

  constructor() {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      const url: string = e.urlAfterRedirects || e.url;
      const match = Object.keys(this.ROUTE_NAMES).find(k => url.startsWith(k));
      this.pageName = match ? this.ROUTE_NAMES[match] : 'Dashboard';
    });
  }

  initials = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '?';
    return (user.firstName[0] + user.lastName[0]).toUpperCase();
  });

  userFullName = computed(() => {
    const user = this.authService.currentUser();
    return user ? `${user.firstName} ${user.lastName}` : 'Guest';
  });

  logout(): void {
    this.authService.logout();
  }
}
