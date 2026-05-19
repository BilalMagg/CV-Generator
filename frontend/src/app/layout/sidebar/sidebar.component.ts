import { Component, inject, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../services/auth.service';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
  accent?: boolean;
  group: 'main' | 'library';
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  private router = inject(Router);
  private authService = inject(AuthService);

  navMain: NavItem[] = [
    { label: 'Dashboard',    route: '/applications/dashboard', icon: 'dashboard', exact: false, group: 'main' },
    { label: 'Generate CV',  route: '/applications/dashboard', icon: 'sparkle',   exact: false, accent: true, group: 'main' },
    { label: 'Applications', route: '/applications/kanban',    icon: 'kanban',    exact: false, group: 'main' },
    { label: 'Analytics',    route: '/applications/analytics', icon: 'analytics', exact: false, group: 'main' },
    { label: 'Calendar',     route: '/applications/calendar',  icon: 'calendar',  exact: false, group: 'main' },
  ];

  navLibrary: NavItem[] = [
    { label: 'My CV', route: '/my-cv', icon: 'user', exact: false, group: 'library' },
  ];

  initials = computed(() => {
    const user = this.authService.currentUser();
    if (!user) return '?';
    return (user.firstName[0] + user.lastName[0]).toUpperCase();
  });

  userFullName = computed(() => {
    const user = this.authService.currentUser();
    return user ? `${user.firstName} ${user.lastName}` : 'Guest';
  });

  userEmail = computed(() => {
    const user = this.authService.currentUser();
    return user?.email ?? '';
  });

  logout(): void {
    this.authService.logout();
  }
}
