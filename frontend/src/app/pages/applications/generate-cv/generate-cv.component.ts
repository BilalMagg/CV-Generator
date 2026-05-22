import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

type Tone = 'Confident' | 'Warm' | 'Technical' | 'Concise';

@Component({
  selector: 'app-generate-cv',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './generate-cv.component.html',
  styleUrl: './generate-cv.component.scss',
})
export class GenerateCvComponent {
  jobDescription = signal('');
  selectedTone = signal<Tone>('Confident');
  emailResult = signal(true);
  generating = signal(false);

  tones: Tone[] = ['Confident', 'Warm', 'Technical', 'Concise'];

  constructor(private router: Router) {}

  setTone(t: Tone) { this.selectedTone.set(t); }

  useSample() {
    this.jobDescription.set(`We're looking for a Senior Product Designer to join our team.

You'll work closely with engineering and product to design experiences for millions of users. You'll own the design process end-to-end — from research and ideation to high-fidelity mockups and QA.

Requirements:
- 5+ years of product design experience
- Proficiency in Figma and Protopie
- Experience working in agile teams
- Strong portfolio demonstrating systems thinking`);
  }

  generate() {
    if (!this.jobDescription().trim()) return;
    this.generating.set(true);
    setTimeout(() => {
      this.generating.set(false);
    }, 2000);
  }

  goToHistory() { this.router.navigate(['/applications/resumes']); }
}
