import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { APP_NAME } from '../../../../../app-name';

@Component({
  selector: 'app-testimonials-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './testimonials.component.html',
  styleUrl: './testimonials.component.scss',
})
export class TestimonialsSectionComponent {
  appName = APP_NAME;
  stats = [
    { num: '3.2x', label: 'more callbacks' },
    { num: '11s',  label: 'avg. CV generation' },
    { num: '6w',   label: 'average time to offer' },
  ];
}
