import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-resumes',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page">
      <div style="padding:2rem;text-align:center;color:var(--color-text-tertiary)">
        <i class="ti ti-file-cv" style="font-size:48px;display:block;margin-bottom:1rem;opacity:.4"></i>
        <h3>Resume Performance</h3>
        <p style="margin-top:.5rem">Coming soon — compare CV versions and track response rates.</p>
      </div>
    </div>
  `
})
export class ResumesComponent {}
