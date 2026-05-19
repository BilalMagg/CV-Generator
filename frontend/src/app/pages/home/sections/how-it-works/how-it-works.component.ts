import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-how-it-works-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './how-it-works.component.html',
  styleUrl: './how-it-works.component.scss',
})
export class HowItWorksSectionComponent {
  steps = [
    { num: '01', title: 'Build your portfolio', desc: 'Drop in your projects, skills, and experience. We structure everything for reuse.' },
    { num: '02', title: 'Paste any job description', desc: 'From any company. We extract requirements and surface gaps.' },
    { num: '03', title: 'CV in eleven seconds', desc: 'With a cover letter, ATS score, and a match analysis — emailed to you.' },
    { num: '04', title: 'Track every step', desc: 'Move applications through stages. Get reminders. Watch your funnel.' },
  ];
}
