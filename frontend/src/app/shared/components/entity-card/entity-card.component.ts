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
             <path d="M20 6L9 17L4 12" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
           </svg>
        </div>
      </div>
      
      <div class="card-body">
        <h3 class="title">{{ title }}</h3>
        <p class="subtitle" *ngIf="subtitle">{{ subtitle }}</p>
        
        <div class="meta" *ngIf="meta">
          <span class="meta-item">
            <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" class="icon">
              <path d="M12 11C13.6569 11 15 9.65685 15 8C15 6.34315 13.6569 5 12 5C10.3431 5 9 6.34315 9 8C9 9.65685 10.3431 11 12 11Z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
              <path d="M12 11C9.79086 11 8 12.7909 8 15V17H16V15C16 12.7909 14.2091 11 12 11Z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>
            {{ meta }}
          </span>
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
      background: #1a1a1a;
      border-radius: 12px;
      padding: 1.25rem;
      border: 1px solid #333;
      transition: all 0.2s;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      position: relative;
      height: 100%;
    }

    .entity-card:hover {
      border-color: #3b82f6;
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.5);
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .entity-type {
      font-size: 0.75rem;
      color: #666;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      font-weight: 600;
    }

    .status-indicator {
      width: 20px;
      height: 20px;
      background: #064e3b;
      color: #34d399;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .status-indicator svg {
      width: 12px;
      height: 12px;
    }

    .card-body .title {
      font-size: 1.1rem;
      font-weight: 700;
      color: #fff;
      margin-bottom: 0.25rem;
    }

    .card-body .subtitle {
      font-size: 0.875rem;
      color: #999;
      margin-bottom: 0.5rem;
    }

    .meta {
      margin-top: 0.5rem;
    }

    .meta-item {
      font-size: 0.8rem;
      color: #888;
      display: flex;
      align-items: center;
      gap: 0.35rem;
    }

    .icon {
      width: 14px;
      height: 14px;
    }

    .card-footer {
      margin-top: auto;
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding-top: 0.75rem;
      border-top: 1px solid #333;
    }

    .evaluation {
      font-size: 0.8rem;
      font-weight: 600;
      color: #34d399;
    }

    .date {
      font-size: 0.75rem;
      color: #666;
    }

    .status-completed {
      border-left: 4px solid #10b981;
    }
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
