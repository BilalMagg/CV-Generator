import { Component } from '@angular/core';

@Component({
  selector: 'app-education',
  standalone: true,
  imports: [],
  template: `
    <div class="page">
      <h1>Education</h1>
      <p>Your education entries will appear here.</p>
    </div>
  `,
  styles: [`
    .page { max-width: 900px; }
    h1 { margin-bottom: 0.5rem; }
    p { color: #888; }
  `]
})
export class EducationComponent {}
