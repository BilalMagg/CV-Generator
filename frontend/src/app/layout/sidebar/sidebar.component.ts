import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

interface NavItem {
  label: string;
  route: string;
  active?: boolean;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss'
})
export class SidebarComponent {
  navItems: NavItem[] = [
    { label: 'Experience',    route: '/experience',    active: true },
    { label: 'Personal Info', route: '/personal-info'              },
    { label: 'Education',     route: '/education'                  },
    { label: 'Skills',        route: '/skills'                     },
  ];
}