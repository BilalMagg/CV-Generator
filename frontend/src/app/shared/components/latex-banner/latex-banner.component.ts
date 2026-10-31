import { Component, inject, signal } from '@angular/core';
import { CapabilitiesService } from '@app/services/capabilities.service';

/**
 * Global notice shown once the deployment reports no LaTeX engine:
 * CV-PDF compilation is disabled, but .tex generation still works.
 * Dismissible per page load (a dismissed engine-less session reappears
 * fresh next time; a restore of the engine clears it automatically).
 */
@Component({
  selector: 'app-latex-banner',
  standalone: true,
  template: `
    @if (visible()) {
      <div class="latex-banner" role="status">
        <svg viewBox="0 0 24 24" fill="none" width="16" height="16" aria-hidden="true">
          <path d="M12 9v4M12 16v-0.5M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z"
                stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        <span class="lb-text">
          <strong>LaTeX engine unavailable</strong> — CV PDF compilation is disabled on this deployment.
          CV <code>.tex</code> files are still generated and can be compiled on Overleaf.
        </span>
        <button type="button" class="lb-close" (click)="dismiss()" title="Dismiss" aria-label="Dismiss">
          <svg viewBox="0 0 24 24" fill="none" width="14" height="14"><path d="M18 6 6 18M6 6l12 12" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>
        </button>
      </div>
    }
  `,
  styles: [`
    .latex-banner {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 9px 14px;
      margin: 10px 18px 0;
      border: 1px solid var(--border, #e2e8f0);
      border-left: 3px solid #f59e0b;
      border-radius: 8px;
      background: var(--surface, #fff);
      color: var(--text, #0f172a);
      font-size: 12.5px;
      box-shadow: var(--shadow-sm, 0 1px 2px rgb(0 0 0 / 0.05));
    }
    .latex-banner svg { flex-shrink: 0; color: #f59e0b; }
    .lb-text strong { font-weight: 600; }
    .lb-text code {
      font-size: 11.5px;
      background: var(--bg, #f8fafc);
      border: 1px solid var(--border, #e2e8f0);
      border-radius: 4px;
      padding: 0 4px;
    }
    .lb-close {
      margin-left: auto;
      flex-shrink: 0;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 22px;
      height: 22px;
      border: none;
      border-radius: 5px;
      background: transparent;
      color: var(--text-secondary, #64748b);
      cursor: pointer;
    }
    .lb-close:hover { background: var(--bg-hover, #f1f5f9); color: var(--text-primary, #0f172a); }
  `],
})
export class LatexBannerComponent {
  private readonly capabilities = inject(CapabilitiesService);
  private readonly dismissed = signal(false);

  readonly visible = signal(false);

  constructor() {
    void this.capabilities.load().then(() => {
      this.visible.set(this.capabilities.latexAvailable() === false && !this.dismissed());
    });
  }

  dismiss(): void {
    this.dismissed.set(true);
    this.visible.set(false);
  }
}
