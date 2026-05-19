import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-testimonials-section',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './testimonials.component.html',
  styleUrl: './testimonials.component.scss',
})
export class TestimonialsSectionComponent {
  stats = [
    { num: '3.2x', label: 'more callbacks' },
    { num: '11s',  label: 'avg. CV generation' },
    { num: '6w',   label: 'average time to offer' },
  ];
}
