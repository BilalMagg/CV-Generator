import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-hero-section',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './hero.component.html',
  styleUrl: './hero.component.scss',
})
export class HeroSectionComponent {
  fauxNavItems = [
    { label: 'Dashboard',    active: true },
    { label: 'Generate CV',  active: false },
    { label: 'Applications', active: false },
    { label: 'Analytics',    active: false },
    { label: 'Calendar',     active: false },
    { label: 'My CV',        active: false },
  ];

  fauxKpis = [
    { label: 'Total apps',  value: '24', pct: '50%', color: 'oklch(0.18 0.01 80)' },
    { label: 'Interviewing', value: '6', pct: '70%', color: 'oklch(0.6 0.16 250)' },
    { label: 'Offers',       value: '3', pct: '30%', color: 'oklch(0.55 0.16 145)' },
    { label: 'Response',    value: '38%', pct: '60%', color: 'oklch(0.6 0.16 30)' },
  ];
}
