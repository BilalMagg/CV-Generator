import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpService } from '@app/services/http.service';
import { LinkedInToolService } from '@app/services/linkedin-tool.service';
import { ToastService } from '@app/services/toast.service';
import { ApiResponse } from '@app/models/application.model';
import {
  LinkedInLength,
  LinkedInRequest,
  LinkedInTone,
  LinkedInToolType,
  LinkedInVariant,
} from '@app/models/tools.model';

interface CareerItem {
  key: string;
  label: string;
  text: string;
  source: 'project' | 'experience';
}

interface Option {
  value: string;
  label: string;
}

@Component({
  selector: 'app-tools-hub',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './tools-hub.component.html',
  styleUrl: './tools-hub.component.scss',
})
export class ToolsHubComponent {
  private readonly service = inject(LinkedInToolService);
  private readonly http = inject(HttpService);
  private readonly toast = inject(ToastService);

  tool = signal<LinkedInToolType>('post');

  // Shared options
  tone = signal<LinkedInTone>('professional');
  length = signal<LinkedInLength>('medium');
  variants = signal(2);
  language = signal('English');

  // Post
  contextType = signal('project');
  contextText = signal('');
  mentionsText = signal('');
  includeHashtags = signal(true);
  hashtagCount = signal(3);

  // Comment
  targetText = signal('');
  pointsText = signal('');

  // Message
  recipientName = signal('');
  relationship = signal('network');
  purpose = signal('introduction');
  recipientContext = signal('');
  senderContext = signal('');

  // Run state
  loading = signal(false);
  error = signal('');
  results = signal<LinkedInVariant[]>([]);
  generatedTool = signal<LinkedInToolType>('post');

  // Career picker
  careerItems = signal<CareerItem[]>([]);
  careerLoading = signal(false);
  careerLoaded = signal(false);

  readonly tones: Option[] = [
    { value: 'professional', label: 'Professional' },
    { value: 'enthusiastic', label: 'Enthusiastic' },
    { value: 'storytelling', label: 'Storytelling' },
    { value: 'honest', label: 'Honest & casual' },
  ];
  readonly lengths: Option[] = [
    { value: 'short', label: 'Short' },
    { value: 'medium', label: 'Medium' },
    { value: 'long', label: 'Long' },
  ];
  readonly contextTypes: Option[] = [
    { value: 'hackathon', label: 'Hackathon' },
    { value: 'project', label: 'Project' },
    { value: 'event', label: 'Event' },
    { value: 'achievement', label: 'Achievement' },
    { value: 'learning', label: 'Learning' },
    { value: 'other', label: 'Other' },
  ];
  readonly relationships: Option[] = [
    { value: 'network', label: 'LinkedIn connection (barely know)' },
    { value: 'colleague', label: 'Current / former colleague' },
    { value: 'alumni', label: 'School alumnus' },
    { value: 'event', label: 'Met at an event' },
    { value: 'other', label: 'Other' },
  ];
  readonly purposes: Option[] = [
    { value: 'introduction', label: 'Introduction' },
    { value: 'thanks', label: 'Thank them' },
    { value: 'follow-up', label: 'Follow up' },
    { value: 'referral', label: 'Ask for referral / intro' },
    { value: 'coffee_chat', label: 'Coffee chat' },
    { value: 'other', label: 'Other' },
  ];
  readonly languages = ['English', 'French', 'Arabic'];

  readonly tabs: { value: LinkedInToolType; label: string }[] = [
    { value: 'post', label: 'Post' },
    { value: 'comment', label: 'Comment' },
    { value: 'message', label: 'Message' },
  ];

  splitLines(text: string): string[] {
    return text.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
  }

  async generate(): Promise<void> {
    let request: LinkedInRequest;
    try {
      request = this.buildRequest();
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Please fill the required fields');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    try {
      const res = await this.service.generate(request);
      const variants = res.data?.variants ?? [];
      if (variants.length === 0) {
        this.error.set('The assistant returned nothing usable — try again or rephrase the context.');
      } else {
        this.results.set(variants);
        this.generatedTool.set(request.tool);
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Generation failed. Check that the AI service is running.');
    } finally {
      this.loading.set(false);
    }
  }

  private buildRequest(): LinkedInRequest {
    const base = {
      language: this.language(),
      tone: this.tone(),
      length: this.length(),
      variants: this.variants(),
    };
    switch (this.tool()) {
      case 'post': {
        const context = this.contextText().trim();
        if (!context) throw new Error('Describe what the post is about so the assistant has material to write');
        return {
          ...base,
          tool: 'post',
          contextType: this.contextType(),
          context,
          mentions: this.splitLines(this.mentionsText()).slice(0, 10),
          includeHashtags: this.includeHashtags(),
          hashtagCount: this.hashtagCount(),
        };
      }
      case 'comment': {
        if (!this.targetText().trim()) throw new Error('Paste the post or comment you want to reply to');
        return {
          ...base,
          tool: 'comment',
          targetText: this.targetText().trim(),
          points: this.splitLines(this.pointsText()),
        };
      }
      case 'message': {
        if (!this.recipientName().trim()) throw new Error('Enter the recipient name');
        return {
          ...base,
          tool: 'message',
          recipientName: this.recipientName().trim(),
          relationship: this.relationship(),
          purpose: this.purpose(),
          recipientContext: this.recipientContext().trim(),
          senderContext: this.senderContext().trim(),
        };
      }
    }
  }

  copy(variant: LinkedInVariant): void {
    const text = [variant.title, variant.text, variant.hashtags].filter(Boolean).join('\n\n');
    navigator.clipboard?.writeText(text).then(
      () => this.toast.success('Copied to clipboard'),
      () => this.toast.error('Could not copy'),
    );
  }

  async loadCareer(): Promise<void> {
    if (this.careerLoaded() || this.careerLoading()) return;
    this.careerLoading.set(true);
    try {
      const [projectsRes, experiencesRes] = await Promise.all([
        this.http.get<ApiResponse<any[]>>('/api/user-content/projects'),
        this.http.get<ApiResponse<any[]>>('/api/user-content/experiences'),
      ]);
      const items: CareerItem[] = [];
      for (const p of projectsRes.data ?? []) {
        const body = [p.description, p.achievementsJson ? JSON.parse(p.achievementsJson) : '']
          .filter(Boolean)
          .join(' — ');
        items.push({
          key: `project:${p.id}`,
          label: `[Project] ${p.title}${p.role ? ` (${p.role})` : ''}`,
          text: `Project "${p.title}"${p.role ? ` (role: ${p.role})` : ''}:\n${body}`.trim(),
          source: 'project',
        });
      }
      for (const e of experiencesRes.data ?? []) {
        const company = e.company ? ` at ${e.company}` : '';
        items.push({
          key: `experience:${e.id}`,
          label: `[Experience] ${e.title}${company}`,
          text: `Experience: ${e.title}${company}${e.description ? `:\n${e.description}` : ''}`,
          source: 'experience',
        });
      }
      this.careerItems.set(items);
      this.careerLoaded.set(true);
    } catch {
      this.toast.error('Could not load your career content');
    } finally {
      this.careerLoading.set(false);
    }
  }

  pickCareer(item: CareerItem): void {
    if (this.tool() === 'post') {
      this.contextType.set(item.source);
      this.contextText.set(item.text);
      this.toast.info(`Filled post context from ${item.label}`);
    } else {
      this.senderContext.set(item.text);
      this.toast.info('Filled sender context (what to say about you)');
    }
  }
}