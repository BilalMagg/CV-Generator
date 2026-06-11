import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '@app/services/auth.service';
import { TemplateAgentService, RenderedCV } from '@app/services/template-agent.service';
import { extractError } from '@app/shared/error-utils';

interface TemplateOption {
  id: string;
  label: string;
}

@Component({
  selector: 'app-template-agent-workspace',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './template-agent-workspace.component.html',
  styleUrl: './template-agent-workspace.component.scss',
})
export class TemplateAgentWorkspaceComponent {
  private readonly authService = inject(AuthService);
  private readonly templateService = inject(TemplateAgentService);

  loading = signal(false);
  error   = signal('');
  result  = signal<RenderedCV | null>(null);

  targetRole = '';
  templateId = 'default';
  summary    = '';

  readonly templates: TemplateOption[] = [
    { id: 'default',   label: 'Auto' },
    { id: 'modern',    label: 'Modern' },
    { id: 'minimal',   label: 'Minimal' },
    { id: 'executive', label: 'Executive' },
    { id: 'academic',  label: 'Academic' },
  ];

  async onRender(): Promise<void> {
    this.error.set('');
    this.result.set(null);
    if (!this.targetRole.trim()) {
      this.error.set('Target role is required');
      return;
    }
    this.loading.set(true);
    try {
      const userId = this.authService.currentUser()!.userId;
      const res = await this.templateService.renderCV({
        user_id: userId,
        target_role: this.targetRole.trim(),
        template_id: this.templateId,
        summary: this.summary.trim() || undefined,
      });
      this.result.set(res);
    } catch (err) {
      this.error.set(extractError(err));
    } finally {
      this.loading.set(false);
    }
  }
}
