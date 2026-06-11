import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '@app/services/auth.service';
import { TemplateAgentService, TemplateDefinition } from '@app/services/template-agent.service';
import { extractError } from '@app/shared/error-utils';

@Component({
  selector: 'app-template-agent-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './template-agent-workspace.component.html',
  styleUrl: './template-agent-workspace.component.scss',
})
export class TemplateAgentWorkspaceComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly templateService = inject(TemplateAgentService);

  // Template gallery
  availableTemplates = signal<TemplateDefinition[]>([]);
  selectedTemplateId = signal('');
  templatesLoading = signal(true);

  // Tone
  tone: 'confident' | 'warm' | 'technical' | 'concise' = 'confident';
  readonly tones = [
    { id: 'confident',  label: 'Confident' },
    { id: 'warm',       label: 'Warm' },
    { id: 'technical',  label: 'Technical' },
    { id: 'concise',    label: 'Concise' },
  ] as const;

  // Advanced settings
  showAdvanced = false;
  manualRole = '';

  // Render state
  loading = signal(false);
  error   = signal('');

  get targetRole(): string {
    return this.manualRole.trim() || 'General';
  }

  readonly canSubmit = computed(() =>
    !this.loading() && !!this.selectedTemplateId()
  );

  async ngOnInit(): Promise<void> {
    const templates = await this.templateService.getTemplates();
    this.availableTemplates.set(templates);
    this.templatesLoading.set(false);
    if (templates.length > 0) this.selectedTemplateId.set(templates[0].id);
  }

  async onRender(): Promise<void> {
    this.error.set('');
    if (!this.selectedTemplateId()) { this.error.set('Please select a template to continue.'); return; }
    this.loading.set(true);
    try {
      const userId = this.authService.currentUser()!.userId;
      const result = await this.templateService.renderCV({
        user_id: userId,
        target_role: this.targetRole,
        template_id: this.selectedTemplateId(),
        tone: this.tone,
      });
      this.router.navigate(['/agents-hub/template-agent/result'], {
        state: { result, targetRole: this.targetRole },
      });
    } catch (err) {
      this.error.set(extractError(err));
    } finally {
      this.loading.set(false);
    }
  }
}
