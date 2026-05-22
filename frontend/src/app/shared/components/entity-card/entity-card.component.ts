import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-entity-card',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="entity-card" [class.status-completed]="isCompleted">
      <div class="card-header">
        <span class="entity-type">{{ typeLabel }}</span>
        <div class="status-indicator" *ngIf="isCompleted">
          <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M20 6L9 17L4 12" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"/>
          </svg>
        </div>
      </div>

      <div class="card-body">
        <h3 class="title">{{ title }}</h3>
        <p class="subtitle" *ngIf="subtitle">{{ subtitle }}</p>
        <div class="meta" *ngIf="meta">
          <span class="meta-item">{{ meta }}</span>
        </div>
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

    .entity-type {
      font-size: 10.5px;
      color: var(--text-3);
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-weight: 600;
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
    }

    .card-body .subtitle {
      font-size: 12.5px;
      color: var(--text-3);
    }

    .meta { margin-top: 4px; }

    .meta-item {
      font-size: 12px;
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
}
