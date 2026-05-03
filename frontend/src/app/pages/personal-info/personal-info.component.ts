import { Component } from '@angular/core';

@Component({
  selector: 'app-personal-info',
  standalone: true,
  imports: [],
  template: `
    <div class="page">
      <h1>Personal Info</h1>
      <p>Your personal information will appear here.</p>
    </div>
  `,
  styles: [`
    .page { max-width: 900px; }
    h1 { margin-bottom: 0.5rem; }
    p { color: #888; }
  `]
})
export class PersonalInfoComponent {}
