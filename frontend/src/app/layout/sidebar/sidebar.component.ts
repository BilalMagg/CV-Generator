import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  route: string;
  icon: string;
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

  navItems: NavItem[] = [
    { label: 'Applications',  route: '/applications',  icon: 'applications' },
    { label: 'Experience',    route: '/experience',    icon: 'experience' },
    { label: 'Personal Info', route: '/personal-info', icon: 'personal' },
    { label: 'Education',     route: '/education',     icon: 'education' },
    { label: 'Skills',        route: '/skills',        icon: 'skills' },
  ];

  isActive(route: string): boolean {
    return this.router.url === route;
  }
}
