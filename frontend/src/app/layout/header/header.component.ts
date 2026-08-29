import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { AuthService } from '@app/services/auth.service';
import { APP_NAME } from '@app/app-name';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss'
})
export class HeaderComponent {
  appName = APP_NAME;
  private authService = inject(AuthService);
  private router = inject(Router);

  pageName = 'Dashboard';

  private readonly ROUTE_NAMES: Record<string, string> = {
    '/applications/dashboard': 'Dashboard',
    '/applications/generate':  'Generate CV',
    '/applications/kanban':    'Applications',
    '/applications/list':      'Applications',
    '/applications/analytics': 'Analytics',
    '/applications/calendar':  'Calendar',
    '/my-career':              'My Career',
    '/agents-hub':             'Agents Hub',
    '/agents-hub/guide':       'Agent Guide',
    '/agents-hub/job-crawler': 'Job Crawler',
    '/agents-hub/template-agent': 'Template Agent',
    '/job-offers':             'Job Offers',
    '/settings':               'Settings',
  };

  constructor() {
    this.router.events.pipe(
      filter(e => e instanceof NavigationEnd)
    ).subscribe((e: any) => {
      const url: string = e.urlAfterRedirects || e.url;
      const match = Object.keys(this.ROUTE_NAMES)
        .sort((a, b) => b.length - a.length)
        .find(k => url.startsWith(k));
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
