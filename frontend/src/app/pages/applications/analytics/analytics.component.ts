import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page">
      <div style="padding:2rem;text-align:center;color:var(--color-text-tertiary)">
        <i class="ti ti-chart-bar" style="font-size:48px;display:block;margin-bottom:1rem;opacity:.4"></i>
        <h3>Analytics & Insights</h3>
        <p style="margin-top:.5rem">Coming soon — funnel charts, timelines, and smart insights.</p>
      </div>
    </div>
  `
})
export class AnalyticsComponent {}
