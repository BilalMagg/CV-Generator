import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-pipeline-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './pipeline.component.html',
  styleUrl: './pipeline.component.scss',
})
export class PipelineSectionComponent {
  kanbanCols = [
    { label: 'Applied',   color: 'oklch(0.7 0.015 250)', itemColor: 'oklch(0.5 0.15 250)', items: ['Vercel','Stripe','Ramp'] },
    { label: 'Interview', color: 'oklch(0.6 0.16 250)',  itemColor: 'oklch(0.55 0.16 250)',items: ['Linear','Notion'] },
    { label: 'Offer',     color: 'oklch(0.55 0.16 145)', itemColor: 'oklch(0.5 0.16 145)', items: ['Modal'] },
  ];

  analyticsSeries = [
    { label: 'Applied',   val: 8, pct: '100%', color: 'oklch(0.7 0.015 250)' },
    { label: 'Interview', val: 6, pct: '75%',  color: 'oklch(0.6 0.16 250)' },
    { label: 'Offer',     val: 3, pct: '38%',  color: 'oklch(0.55 0.16 145)' },
    { label: 'Rejected',  val: 4, pct: '50%',  color: 'oklch(0.65 0.18 25)' },
  ];

  dayLabels = ['M','T','W','T','F','S','S'];

  calDays = [
    { num: 14, today: false, event: null },
    { num: 15, today: false, event: 'oklch(0.6 0.16 250)' },
    { num: 16, today: false, event: null },
    { num: 17, today: false, event: 'oklch(0.6 0.16 30)' },
    { num: 18, today: true,  event: 'oklch(0.55 0.18 268)' },
    { num: 19, today: false, event: null },
    { num: 20, today: false, event: 'oklch(0.55 0.16 145)' },
  ];
}
