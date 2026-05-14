import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TabBarComponent, TabItem } from '../../shared/components/tab-bar/tab-bar.component';

@Component({
  selector: 'app-applications-layout',
  standalone: true,
  imports: [RouterOutlet, TabBarComponent],
  template: `
    <app-tab-bar [tabs]="atsTabs" baseRoute="/applications" />
    <div class="ats-content">
      <router-outlet />
    </div>
  `,
  styles: [`
    .ats-content { flex: 1; overflow-y: auto; }
  `]
})
export class ApplicationsLayoutComponent {
  atsTabs: TabItem[] = [
    { label: 'Dashboard',     route: 'dashboard',  icon: 'layout-dashboard' },
    { label: 'Applications',  route: 'list',       icon: 'list' },
    { label: 'Kanban',        route: 'kanban',     icon: 'layout-kanban' },
    { label: 'Analytics',     route: 'analytics',  icon: 'chart-bar' },
    { label: 'Calendar',      route: 'calendar',   icon: 'calendar' },
    { label: 'Resumes',       route: 'resumes',    icon: 'file-cv' },
  ];
}
