import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpService } from '@app/services/http.service';
import { APP_NAME } from '@app/app-name';

type Tone = 'Confident' | 'Warm' | 'Technical' | 'Concise';

@Component({
  selector: 'app-generate-cv',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './generate-cv.component.html',
  styleUrl: './generate-cv.component.scss',
})
export class GenerateCvComponent {
  appName = APP_NAME;
  private http = inject(HttpService);
  private router = inject(Router);

  jobDescription = signal('');
  selectedTone = signal<Tone>('Confident');
  emailResult = signal(true);
  generating = signal(false);
  error = signal('');

  tones: Tone[] = ['Confident', 'Warm', 'Technical', 'Concise'];

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

  async generate() {
    if (!this.jobDescription().trim()) return;
    this.generating.set(true);
    this.error.set('');
    try {
      await this.http.post('/api/workflows/generate-cv', {
        jobDescription: this.jobDescription(),
        tone: this.selectedTone(),
      });
    } catch (e: any) {
      this.error.set(e?.error?.message || 'Generation failed. Please try again.');
    } finally {
      this.generating.set(false);
    }
  }

  goToHistory() { this.router.navigate(['/applications/resumes']); }
}
