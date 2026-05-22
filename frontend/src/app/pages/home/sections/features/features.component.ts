import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-features-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './features.component.html',
  styleUrl: './features.component.scss',
})
export class FeaturesSectionComponent {
  challenges = [
    {
      title: 'Tailored applications',
      icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="none">
        <circle cx="10" cy="6" r="2.5" stroke="oklch(0.6 0.16 30)" stroke-width="1.6"/>
        <circle cx="5" cy="14" r="2.5" stroke="oklch(0.6 0.16 30)" stroke-width="1.6"/>
        <circle cx="15" cy="14" r="2.5" stroke="oklch(0.6 0.16 30)" stroke-width="1.6"/>
        <path d="M10 8.5 L 7 11.5 M 10 8.5 L 13 11.5" stroke="oklch(0.6 0.16 30)" stroke-width="1.6"/>
      </svg>`,
      body: 'Every application tailored — no more sending the same CV to twelve different roles and hoping.',
    },
    {
      title: 'One workspace',
      icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="none">
        <path d="M3 5 L 5 7 L 9 3" stroke="oklch(0.6 0.16 30)" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M3 11 L 5 13 L 9 9" stroke="oklch(0.6 0.16 30)" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M3 17 L 5 19 L 9 15" stroke="oklch(0.6 0.16 30)" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
        <path d="M12 5 L 18 5 M 12 11 L 18 11 M 12 17 L 18 17" stroke="oklch(0.6 0.16 30)" stroke-width="1.6" stroke-linecap="round"/>
      </svg>`,
      body: 'One workspace for every application — kanban, list, calendar, analytics. No spreadsheets, ever.',
    },
    {
      title: 'Automated reminders',
      icon: `<svg width="20" height="20" viewBox="0 0 20 20" fill="none">
        <circle cx="10" cy="10" r="7.5" stroke="oklch(0.6 0.16 30)" stroke-width="1.6"/>
        <path d="M10 6 L 10 10 L 13 12" stroke="oklch(0.6 0.16 30)" stroke-width="1.6" stroke-linecap="round"/>
      </svg>`,
      body: 'Automated follow-up reminders so no warm lead goes cold while you\'re focused on the next role.',
    },
  ];
}
