import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page">
      <div style="padding:2rem;text-align:center;color:var(--color-text-tertiary)">
        <i class="ti ti-calendar" style="font-size:48px;display:block;margin-bottom:1rem;opacity:.4"></i>
        <h3>Calendar & Follow-ups</h3>
        <p style="margin-top:.5rem">Coming soon — interviews, reminders, and deadlines.</p>
      </div>
    </div>
  `
})
export class CalendarComponent {}
