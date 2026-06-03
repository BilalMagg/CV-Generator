import { Component, signal, inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { APP_NAME } from '@app/app-name';
import { AuthService } from '@app/services/auth.service';
import { CvGenerationService } from '@app/services/cv-generation.service';
import {
  StepStatus,
  CvGenerationStatus,
  CvGenerationResult,
  CVProfile,
  PageState,
  InputTab,
  TONES,
  LANGUAGES,
  TEMPLATES,
  STEP_ICONS,
} from '@app/models/cv-generation.models';

@Component({
  selector: 'app-generate-cv',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './generate-cv.component.html',
  styleUrl: './generate-cv.component.scss',
})
export class GenerateCvComponent implements OnInit, OnDestroy {
  appName = APP_NAME;
  protected readonly TONES = TONES;
  protected readonly LANGUAGES = LANGUAGES;
  protected readonly TEMPLATES = TEMPLATES;
  protected readonly STEP_ICONS = STEP_ICONS;

  private cvService = inject(CvGenerationService);
  private auth = inject(AuthService);
  private router = inject(Router);

  // State
  state = signal<PageState>('form');
  inputTab = signal<InputTab>('paste');

  // Form fields
  jobDescription = signal('');
  jobUrl = signal('');
  selectedFileName = signal<string | null>(null);
  candidateName = signal(
    this.auth.currentUser()
      ? `${this.auth.currentUser()!.firstName} ${this.auth.currentUser()!.lastName}`
      : '',
  );
  recipientEmail = signal(this.auth.currentUser()?.email ?? '');
  sendEmail = signal(true);
  emailSubject = signal('');
  selectedTone = signal('Confident');
  selectedLanguage = signal('en');
  selectedTemplate = signal('default');
  selectedProfileId = signal<string | null>(null);
  profiles = signal<CVProfile[]>([]);
  profilesLoading = signal(false);

  // Progress
  runId = signal<string | null>(null);
  status = signal<CvGenerationStatus | null>(null);
  result = signal<CvGenerationResult | null>(null);
  elapsedSeconds = signal(0);
  error = signal('');
  showRegeneratePanel = signal(false);

  regenerateJobDesc = signal('');
  regenerateTone = signal('');
  regenerateFocus = signal('');

  private pollTimer: ReturnType<typeof setInterval> | null = null;
  private elapsedTimer: ReturnType<typeof setInterval> | null = null;

  defaultSteps: StepStatus[] = [
    { step: 0, name: 'Job Extraction', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 1, name: 'Profile Matching', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 2, name: 'CV Optimization', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 3, name: 'Template Rendering', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
    { step: 4, name: 'Email Delivery', status: 'pending', started_at: null, completed_at: null, duration_ms: null, error: null },
  ];

  ngOnInit() {
    this.loadProfiles();
  }

  ngOnDestroy() {
    this.stopPolling();
    this.stopElapsedTimer();
  }

  private async loadProfiles() {
    this.profilesLoading.set(true);
    const p = await this.cvService.fetchProfiles();
    this.profiles.set(p);
    this.profilesLoading.set(false);
  }

  formatElapsed(s: number): string {
    const m = Math.floor(s / 60);
    const sec = s % 60;
    return m > 0 ? `${m}m ${sec}s` : `${sec}s`;
  }

  pdfPreviewUrl(): string {
    const render = this.result()?.render;
    return render?.file_path ?? render?.FilePath ?? render?.cv_code ?? render?.CvCode ?? '';
  }

  formatDuration(ms: number): string {
    if (ms < 1000) return `${ms}ms`;
    return `${(ms / 1000).toFixed(1)}s`;
  }

  stepPercent(step: StepStatus): number {
    if (step.status === 'completed') return 100;
    if (step.status === 'running') return 60;
    if (step.status === 'failed') return 100;
    return 0;
  }

  liveMatchScore(): number | null {
    const s = this.result()?.search;
    return s?.match_score ?? s?.MatchScore ?? null;
  }

  liveAtsScore(): number | null {
    const o = this.result()?.optimization;
    return o?.ats_score_after ?? o?.AtsScoreAfter ?? null;
  }

  liveGapSkills(): string[] {
    const s = this.result()?.search;
    return s?.gap_skills ?? s?.GapSkills ?? [];
  }

  // ── File handling ──
  onFileSelected(event: Event) {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.selectedFileName.set(file.name);
      const reader = new FileReader();
      reader.onload = () => this.jobDescription.set(reader.result as string);
      reader.readAsText(file);
    }
  }

  // ── Generate ──
  async generate() {
    if (this.inputTab() === 'paste' && !this.jobDescription().trim()) return;
    if (this.inputTab() === 'url' && !this.jobUrl().trim()) return;

    this.error.set('');
    this.state.set('progress');
    this.elapsedSeconds.set(0);
    this.startElapsedTimer();

    try {
      const runId = await this.cvService.submit({
        jobDescription:
          this.inputTab() === 'paste'
            ? this.jobDescription()
            : this.inputTab() === 'url'
              ? `[URL: ${this.jobUrl()}]`
              : this.jobDescription(),
        candidateName: this.candidateName() || undefined,
        recipientEmail: this.sendEmail() ? this.recipientEmail() || undefined : undefined,
        templateId: this.selectedTemplate() !== 'default' ? this.selectedTemplate() : undefined,
        language: this.selectedLanguage() !== 'en' ? this.selectedLanguage() : undefined,
        tone: this.selectedTone(),
        emailSubject: this.emailSubject() || undefined,
      });
      this.runId.set(runId);
      this.startPolling(runId);
    } catch (e: any) {
      this.error.set(e?.error?.message || e?.message || 'Failed to start generation.');
      this.state.set('form');
      this.stopElapsedTimer();
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
          this.stopElapsedTimer();
          const r = await this.cvService.getResult(runId);
          this.result.set(r);
          this.state.set('results');
        } else if (s.status === 'failed' || s.status === 'cancelled') {
          this.stopPolling();
          this.stopElapsedTimer();
          this.error.set(s.error_message || `Run ${s.status}`);
        }
      } catch {
        this.stopPolling();
        this.stopElapsedTimer();
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

  private startElapsedTimer() {
    this.stopElapsedTimer();
    this.elapsedTimer = setInterval(() => {
      this.elapsedSeconds.update(v => v + 1);
    }, 1000);
  }

  private stopElapsedTimer() {
    if (this.elapsedTimer) {
      clearInterval(this.elapsedTimer);
      this.elapsedTimer = null;
    }
  }

  // ── Cancel ──
  async cancelRun() {
    const id = this.runId();
    if (!id) return;
    try {
      await this.cvService.cancel(id);
    } catch {}
    this.stopPolling();
    this.stopElapsedTimer();
    this.state.set('form');
  }

  // ── Actions ──
  goBack() {
    this.stopPolling();
    this.stopElapsedTimer();
    this.state.set('form');
    this.runId.set(null);
    this.status.set(null);
    this.result.set(null);
    this.error.set('');
  }

  downloadPdf() {
    const render = this.result()?.render;
    const url = render?.file_path ?? render?.cv_code ?? render?.FilePath ?? render?.CvCode;
    if (url) window.open(url, '_blank');
  }

  viewEditPage() {
    const id = this.runId();
    if (id) this.router.navigate(['/applications/generate/edit', id]);
  }

  saveToApplications() {
    // Future: create an application from the generated CV
    this.router.navigate(['/applications/new']);
  }

  openRegeneratePanel() {
    this.regenerateJobDesc.set(this.jobDescription());
    this.regenerateTone.set(this.selectedTone());
    this.regenerateFocus.set('');
    this.showRegeneratePanel.set(true);
  }

  closeRegeneratePanel() {
    this.showRegeneratePanel.set(false);
  }

  async regenerate() {
    this.showRegeneratePanel.set(false);
    this.result.set(null);

    const focusText = this.regenerateFocus()
      ? `\n\n[Modifications: ${this.regenerateFocus()}]`
      : '';

    this.jobDescription.set(this.regenerateJobDesc() + focusText);
    this.selectedTone.set(this.regenerateTone());

    this.state.set('progress');
    this.elapsedSeconds.set(0);
    this.startElapsedTimer();

    try {
      const runId = await this.cvService.submit({
        jobDescription: this.jobDescription(),
        candidateName: this.candidateName() || undefined,
        recipientEmail: this.sendEmail() ? this.recipientEmail() || undefined : undefined,
        templateId: this.selectedTemplate() !== 'default' ? this.selectedTemplate() : undefined,
        language: this.selectedLanguage() !== 'en' ? this.selectedLanguage() : undefined,
        tone: this.selectedTone(),
        emailSubject: this.emailSubject() || undefined,
      });
      this.runId.set(runId);
      this.startPolling(runId);
    } catch (e: any) {
      this.error.set(e?.message || 'Regeneration failed.');
      this.state.set('results');
      this.stopElapsedTimer();
    }
  }

  useSample() {
    this.jobDescription.set(`We're looking for a Senior Product Designer to join our team.

You'll work closely with engineering and product to design experiences for millions of users. You'll own the design process end-to-end — from research and ideation to high-fidelity mockups and QA.

Requirements:
- 5+ years of product design experience
- Proficiency in Figma and Protopie
- Experience working in agile teams
- Strong portfolio demonstrating systems thinking`);
    this.inputTab.set('paste');
  }

  goToHistory() {
    this.router.navigate(['/applications/resumes']);
  }
}
