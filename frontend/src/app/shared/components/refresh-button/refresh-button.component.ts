import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-refresh-button',
  standalone: true,
  template: `
    <button class="refresh-btn" [class.spinning]="loading()" (click)="refresh.emit()" [title]="label()">
      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none"
           stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <path d="M21 2v6h-6"/><path d="M3 12a9 9 0 0 1 15-6.7L21 8"/>
        <path d="M3 22v-6h6"/><path d="M21 12a9 9 0 0 1-15 6.7L3 16"/>
      </svg>
    </button>
  `,
  styles: [`
    .refresh-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 32px;
      height: 32px;
      border: 1px solid var(--border, #e2e8f0);
      border-radius: 6px;
      background: var(--bg-btn, #fff);
      color: var(--text-secondary, #64748b);
      cursor: pointer;
      transition: all 0.15s;
      flex-shrink: 0;
    }
    .refresh-btn:hover {
      background: var(--bg-hover, #f1f5f9);
      color: var(--text-primary, #0f172a);
      border-color: var(--border-hover, #cbd5e1);
    }
    .spinning svg {
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      from { transform: rotate(0deg); }
      to { transform: rotate(360deg); }
    }
  `]
})
export class RefreshButtonComponent {
  loading = input(false);
  label = input('Refresh');
  refresh = output<void>();
}
