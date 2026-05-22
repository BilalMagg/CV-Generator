import { Component } from '@angular/core';

@Component({
  selector: 'app-resumes',
  standalone: true,
  template: `
    <div class="resumes-shell">
      <div class="resumes-placeholder">
        <div class="placeholder-icon">
          <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round">
            <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/><line x1="10" y1="9" x2="8" y2="9"/>
          </svg>
        </div>
        <h3 class="placeholder-title">Resume Performance</h3>
        <p class="placeholder-sub">Compare CV versions and track response rates — coming soon.</p>
      </div>
    </div>
  `,
  styles: [`
    .resumes-shell {
      display: flex;
      flex: 1;
      align-items: center;
      justify-content: center;
      padding: 40px;
      background: var(--bg);
    }
    .resumes-placeholder { text-align: center; }
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
export class ResumesComponent {}
