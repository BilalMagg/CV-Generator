import { Component } from '@angular/core';

@Component({
  selector: 'app-experience',
  standalone: true,
  imports: [],
  template: `
    <div class="page">
      <h1>Experience</h1>
      <p>Your work experience entries will appear here.</p>
    </div>
  `,
  styles: [`
    .page { max-width: 900px; }
    h1 { margin-bottom: 0.5rem; }
    p { color: #888; }
  `]
})
export class ExperienceComponent {}
