import { Component } from '@angular/core';

@Component({
  selector: 'app-analytics',
  standalone: true,
  template: `
    <div class="analytics-shell">
      <div class="analytics-placeholder">
        <div class="placeholder-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
            <line x1="18" y1="20" x2="18" y2="10"/><line x1="12" y1="20" x2="12" y2="4"/><line x1="6" y1="20" x2="6" y2="14"/>
          </svg>
        </div>
        <h3 class="placeholder-title">Analytics & Insights</h3>
        <p class="placeholder-sub">Funnel charts, timelines, and smart insights — coming soon.</p>
      </div>
    </div>
  `,
  styles: [`
    .analytics-shell {
      display: flex;
      flex: 1;
      align-items: center;
      justify-content: center;
      padding: 40px;
      background: var(--bg);
    }
    .analytics-placeholder { text-align: center; }
    .placeholder-icon {
      width: 60px;
      height: 60px;
      border-radius: 16px;
      background: white;
      border: 1px solid oklch(0.92 0.004 80);
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0 auto 18px;
      color: var(--text-3);
    }
    .placeholder-title { font-size: 18px; font-weight: 700; color: var(--text); font-family: var(--font-display); letter-spacing: -0.02em; margin: 0 0 8px; }
    .placeholder-sub { font-size: 14px; color: var(--text-3); margin: 0; }
  `]
})
export class AnalyticsComponent {}
