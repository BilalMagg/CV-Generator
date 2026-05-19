import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-logos-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './logos.component.html',
  styleUrl: './logos.component.scss',
})
export class LogosSectionComponent {
  companies = [
    { name: 'Linear',    mark: 'L', color: 'oklch(0.5 0.15 265)' },
    { name: 'Stripe',    mark: 'S', color: 'oklch(0.5 0.15 250)' },
    { name: 'Vercel',    mark: 'V', color: 'oklch(0.22 0.01 80)'  },
    { name: 'Notion',    mark: 'N', color: 'oklch(0.35 0.01 80)'  },
    { name: 'Figma',     mark: 'F', color: 'oklch(0.6 0.18 25)'   },
    { name: 'Ramp',      mark: 'R', color: 'oklch(0.5 0.15 145)'  },
    { name: 'Anthropic', mark: 'A', color: 'oklch(0.55 0.16 30)'  },
  ];
}
