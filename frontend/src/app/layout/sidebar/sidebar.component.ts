import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  exact?: boolean;
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
    { label: 'Applications',  route: '/applications/dashboard',  icon: 'applications', exact: false },
    { label: 'My CV',         route: '/my-cv',                   icon: 'my-cv',        exact: false },
  ];
}
