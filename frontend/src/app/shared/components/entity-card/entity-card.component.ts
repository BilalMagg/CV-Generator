import { Component, Input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-entity-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="entity-card" [class.status-completed]="isCompleted">
      <div class="card-header">
        <span class="entity-type">{{ typeLabel }}</span>
        <div class="header-actions">
          <button
            class="copy-btn"
            type="button"
            *ngIf="copyValue"
            (click)="copy($event)"
            [title]="copied() ? 'Copied' : 'Copy'"
            [class.copied]="copied()"
          >
            <svg *ngIf="!copied()" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <rect x="9" y="9" width="11" height="11" rx="2" stroke="currentColor" stroke-width="2" stroke-linejoin="round"/>
              <path d="M5 15V5a2 2 0 012-2h10" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
            </svg>
            <svg *ngIf="copied()" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M20 6L9 17L4 12" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </button>
          <div class="status-indicator" *ngIf="isCompleted">
            <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M20 6L9 17L4 12" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
          </div>
        </div>
      </div>

      <div class="card-body">
        <h3 class="title">{{ title }}</h3>
        <p class="subtitle" *ngIf="subtitle">{{ subtitle }}</p>
        <div class="meta" *ngIf="meta">
          <span class="meta-item">{{ meta }}</span>
        </div>
      </div>

      <div class="chips" *ngIf="categories && categories.length">
        <span class="chip" *ngFor="let c of visibleCategories()">{{ c }}</span>
        <span class="chip chip-more" *ngIf="extraCount() > 0">+{{ extraCount() }}</span>
      </div>

      <div class="card-footer" *ngIf="footer">
        <span class="evaluation" *ngIf="evaluation">{{ evaluation }}</span>
        <span class="date">{{ footer }}</span>
      </div>
    </div>
  `,
  styles: [`
    .entity-card {
      background: white;
      border-radius: 12px;
      padding: 16px;
      border: 1px solid oklch(0.92 0.004 80);
      box-shadow: 0 1px 0 oklch(0.95 0.004 80);
      transition: border-color 0.12s, box-shadow 0.12s;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      gap: 10px;
      position: relative;
      height: 100%;
    }

    .entity-card:hover {
      border-color: oklch(0.86 0.006 80);
      box-shadow: 0 4px 12px oklch(0.5 0.01 80 / 0.08);
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .header-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .entity-type {
      font-size: 10.5px;
      color: var(--text-3);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-weight: 600;
    }

    // Copy button (entity lists may pass a raw value, e.g. a social-link URL)
    .copy-btn {
      border: none;
      background: transparent;
      color: var(--text-3);
      width: 26px;
      height: 26px;
      border-radius: 8px;
      display: flex;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      padding: 0;
      transition: background 0.12s, color 0.12s;
    }

    .copy-btn svg { width: 14px; height: 14px; }

    .copy-btn:hover {
      background: oklch(0.95 0.004 80);
      color: var(--text);
    }

    // Success state: the copy icon becomes a filled green badge with a white check.
    .copy-btn.copied {
      background: oklch(0.55 0.13 160);
      color: white;
      border-radius: 50%;
      animation: entity-card-copy-pop 0.25s ease;
    }

    .copy-btn.copied:hover {
      background: oklch(0.5 0.13 160);
      color: white;
    }

    @keyframes entity-card-copy-pop {
      0% { transform: scale(0.55); }
      60% { transform: scale(1.15); }
      100% { transform: scale(1); }
    }

    .status-indicator {
      width: 18px;
      height: 18px;
      background: oklch(0.94 0.06 160);
      color: oklch(0.45 0.13 160);
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .status-indicator svg { width: 11px; height: 11px; }

    .card-body .title {
      font-size: 14px;
      font-weight: 600;
      color: var(--text);
      margin-bottom: 3px;
      word-break: break-word;
    }

    .card-body .subtitle {
      font-size: 12.5px;
      color: var(--text-3);
      word-break: break-all;
    }

    .meta { margin-top: 4px; }

    .meta-item {
      font-size: 12px;
      color: var(--text-3);
    }

    .chips {
      display: flex;
      flex-wrap: nowrap;
      gap: 5px;
      margin-top: 2px;
      overflow: hidden;
    }

    .chip {
      font-size: 11px;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 999px;
      background: var(--accent-soft);
      color: var(--accent-text);
      white-space: nowrap;
      flex: 0 0 auto;
    }

    .chip-more {
      background: oklch(0.94 0.004 80);
      color: var(--text-3);
    }

    .card-footer {
      margin-top: auto;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 10px;
      border-top: 1px solid oklch(0.94 0.003 80);
    }

    .evaluation {
      font-size: 12px;
      font-weight: 600;
      color: oklch(0.45 0.13 160);
    }

    .date { font-size: 11.5px; color: var(--text-3); }

    .status-completed { border-left: 3px solid oklch(0.55 0.13 160); }
  `]
})
export class EntityCardComponent {
  @Input() title: string = '';
  @Input() subtitle: string = '';
  @Input() typeLabel: string = '';
  @Input() meta: string = '';
  @Input() evaluation: string = '';
  @Input() footer: string = '';
  @Input() isCompleted: boolean = false;
  @Input() categories: string[] = [];
  /** Raw value a copy button on this card copies (e.g. a social-link URL). Omitted → no button. */
  @Input() copyValue: string | null = null;

  /** How long the success state stays visible after a copy (ms). */
  private static readonly COPIED_FEEDBACK_MS = 1600;

  // Signal (not a plain property): the copy resolves inside an `await`, and in the
  // zoneless app only signal writes schedule the re-render that shows the badge.
  copied = signal(false);
  private copiedTimer: ReturnType<typeof setTimeout> | null = null;

  private static readonly MAX_CHIPS = 3;

  visibleCategories(): string[] {
    return this.categories.slice(0, EntityCardComponent.MAX_CHIPS);
  }

  extraCount(): number {
    return Math.max(0, this.categories.length - EntityCardComponent.MAX_CHIPS);
  }

  async copy(event: Event): Promise<void> {
    event.stopPropagation();
    if (!this.copyValue || this.copied()) return;
    try {
      await navigator.clipboard.writeText(this.copyValue);
      // Turn the button into a success badge for a moment so the copy is unmissable.
      this.copied.set(true);
      if (this.copiedTimer) clearTimeout(this.copiedTimer);
      this.copiedTimer = setTimeout(() => this.copied.set(false), EntityCardComponent.COPIED_FEEDBACK_MS);
    } catch {
      // Clipboard access denied (rare on localhost/https) — the card stays clickable.
    }
  }
}
