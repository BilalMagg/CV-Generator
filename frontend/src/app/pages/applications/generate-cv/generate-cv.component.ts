import { Component, signal, inject, OnDestroy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { APP_NAME } from '@app/app-name';
import { AuthService } from '@app/services/auth.service';
import { CvGenerationService } from '@app/services/cv-generation.service';
import {
  StepStatus,
  CvGenerationStatus,
  CvGenerationResult,
  STEP_ICONS,
} from '@app/models/cv-generation.models';

type PageState = 'form' | 'progress' | 'results';

@Component({
  selector: 'app-generate-cv',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './generate-cv.component.html',
  styleUrl: './generate-cv.component.scss',
})
export class GenerateCvComponent implements OnDestroy {
  appName = APP_NAME;
  private cvService = inject(CvGenerationService);
  private auth = inject(AuthService);
  private router = inject(Router);

  state = signal<PageState>('form');
  jobDescription = signal('');
  candidateName = signal(this.auth.currentUser()
    ? `${this.auth.currentUser()!.firstName} ${this.auth.currentUser()!.lastName}`
    : '');
  recipientEmail = signal(this.auth.currentUser()?.email ?? '');
  sendEmail = signal(true);

  runId = signal<string | null>(null);
  status = signal<CvGenerationStatus | null>(null);
  result = signal<CvGenerationResult | null>(null);
  error = signal('');

  regenerateInput = signal('');
  showRegenerateModal = signal(false);

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnDestroy() {
    this.stopPolling();
  }

  defaultSteps: StepStatus[] = [
    { step: 0, name: 'Job Extraction', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 1, name: 'Profile Matching', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 2, name: 'CV Optimization', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 3, name: 'Template Rendering', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 4, name: 'Email Delivery', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
  ];

  formatDuration(ms: number): string {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  }

  stepIcon(name: string): string {
    return STEP_ICONS[name] ?? 'ti ti-circle';
  }

  async generate() {
    if (!this.jobDescription().trim()) return;
    this.error.set('');
    this.state.set('progress');

    try {
      const runId = await this.cvService.submit({
        jobDescription: this.jobDescription(),
        candidateName: this.candidateName() || undefined,
        recipientEmail: this.sendEmail() ? this.recipientEmail() || undefined : undefined,
      });
      this.runId.set(runId);
      this.startPolling(runId);
    } catch (e: any) {
      this.error.set(e?.error?.message || e?.message || 'Failed to start generation.');
      this.state.set('form');
    }
  }

  private startPolling(runId: string) {
    this.stopPolling();
    this.pollTimer = setInterval(async () => {
      try {
        const s = await this.cvService.getStatus(runId);
        this.status.set(s);
        if (s.status === 'completed') {
          this.stopPolling();
          const r = await this.cvService.getResult(runId);
          this.result.set(r);
          this.state.set('results');
        } else if (s.status === 'failed' || s.status === 'cancelled') {
          this.stopPolling();
          this.error.set(s.error_message || `Run ${s.status}`);
        }
      } catch {
        this.stopPolling();
        this.error.set('Lost connection while polling status.');
      }
    }, 1500);
  }

  private stopPolling() {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  async cancelRun() {
    const id = this.runId();
    if (!id) return;
    try {
      await this.cvService.cancel(id);
    } catch {}
    this.stopPolling();
    this.state.set('form');
  }

  goBack() {
    this.stopPolling();
    this.state.set('form');
    this.runId.set(null);
    this.status.set(null);
    this.result.set(null);
    this.error.set('');
  }

  openRegenerateModal() {
    this.regenerateInput.set('');
    this.showRegenerateModal.set(true);
  }

  closeRegenerateModal() {
    this.showRegenerateModal.set(false);
  }

  async regenerate() {
    this.showRegenerateModal.set(false);
    this.result.set(null);
    this.state.set('progress');
    try {
      const runId = await this.cvService.submit({
        jobDescription: this.jobDescription(),
        candidateName: this.candidateName() || undefined,
        recipientEmail: this.sendEmail() ? this.recipientEmail() || undefined : undefined,
      });
      this.runId.set(runId);
      this.startPolling(runId);
    } catch (e: any) {
      this.error.set(e?.error?.message || e?.message || 'Failed to start generation.');
      this.state.set('results');
    }
  }

  goToHistory() {
    this.router.navigate(['/applications/resumes']);
  }

  useSample() {
    this.jobDescription.set(`We're looking for a Senior Product Designer to join our team.

You'll work closely with engineering and product to design experiences for millions of users. You'll own the design process end-to-end — from research and ideation to high-fidelity mockups and QA.

Requirements:
- 5+ years of product design experience
- Proficiency in Figma and Protopie
- Experience working in agile teams
- Strong portfolio demonstrating systems thinking`);
  }
}
