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
    { name: 'Linear',    slug: 'linear',    color: '5E6AD2' },
    { name: 'Stripe',    slug: 'stripe',    color: '635BFF' },
    { name: 'Vercel',    slug: 'vercel',    color: '000000' },
    { name: 'Notion',    slug: 'notion',    color: '000000' },
    { name: 'Figma',     slug: 'figma',     color: 'F24E1E' },
    { name: 'Ramp',      slug: 'ramp',      color: '00B090' },
    { name: 'Anthropic', slug: 'anthropic', color: '191919' },
  ];

  // Duplicated for seamless infinite marquee
  get marqueeItems() {
    return [...this.companies, ...this.companies];
  }
}
