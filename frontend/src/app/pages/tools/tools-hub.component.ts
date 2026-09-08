import { Component, computed, inject, signal } from '@angular/core';
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

type CareerSource = 'project' | 'experience' | 'hackathon' | 'academicactivity';

interface CareerItem {
  key: string;
  label: string;
  text: string;
  source: CareerSource;
}

interface CareerGroup {
  label: string;
  source: CareerSource;
  items: CareerItem[];
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

  // Shared
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
  careerGroups = signal<CareerGroup[]>([]);
  careerLoading = signal(false);
  careerLoaded = signal(false);
  careerPickerOpen = signal(false);
  selectedKeys = signal<Set<string>>(new Set());

  selectedCount = computed(() => this.selectedKeys().size);

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

  // --- Career picker ---

  toggleCareerPicker(): void {
    const next = !this.careerPickerOpen();
    this.careerPickerOpen.set(next);
    if (next && !this.careerLoaded()) this.loadCareer();
  }

  isKeySelected(key: string): boolean {
    return this.selectedKeys().has(key);
  }

  toggleItem(item: CareerItem): void {
    const set = new Set(this.selectedKeys());
    if (set.has(item.key)) {
      set.delete(item.key);
    } else {
      set.add(item.key);
    }
    this.selectedKeys.set(set);
  }

  selectAllInGroup(group: CareerGroup): void {
    const set = new Set(this.selectedKeys());
    const allSelected = group.items.every((i) => set.has(i.key));
    for (const item of group.items) {
      if (allSelected) {
        set.delete(item.key);
      } else {
        set.add(item.key);
      }
    }
    this.selectedKeys.set(set);
  }

  isGroupFullySelected(group: CareerGroup): boolean {
    return group.items.length > 0 && group.items.every((i) => this.selectedKeys().has(i.key));
  }

  isGroupPartiallySelected(group: CareerGroup): boolean {
    return !this.isGroupFullySelected(group) && group.items.some((i) => this.selectedKeys().has(i.key));
  }

  clearSelection(): void {
    this.selectedKeys.set(new Set());
  }

  /** Compose the full context string from selected career items + free text. */
  private composeContext(): string {
    const allItems = this.careerGroups().flatMap((g) => g.items);
    const selected = allItems.filter((i) => this.selectedKeys().has(i.key));
    const parts: string[] = [];
    for (const item of selected) {
      parts.push(item.text);
    }
    const freeText = this.contextText().trim();
    if (freeText) parts.push(freeText);
    return parts.join('\n\n---\n\n');
  }

  /** Compose sender context from selected career items + free text (for messages). */
  private composeSenderContext(): string {
    const allItems = this.careerGroups().flatMap((g) => g.items);
    const selected = allItems.filter((i) => this.selectedKeys().has(i.key));
    const parts: string[] = selected.map((i) => i.text);
    const freeText = this.senderContext().trim();
    if (freeText) parts.push(freeText);
    return parts.join('\n\n');
  }

  /** Compose post points from selected career items + free text (for comments). */
  private composePoints(): string[] {
    const allItems = this.careerGroups().flatMap((g) => g.items);
    const selected = allItems.filter((i) => this.selectedKeys().has(i.key));
    const parts = selected.map((i) => i.label);
    const freeLines = this.splitLines(this.pointsText());
    return [...parts, ...freeLines];
  }

  // --- Generate ---

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
        const context = this.composeContext();
        if (!context) throw new Error('Add context — select career items or describe what the post is about');
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
        const points = this.composePoints();
        if (!this.targetText().trim() && points.length === 0)
          throw new Error('Paste the post or comment you want to reply to');
        return {
          ...base,
          tool: 'comment',
          targetText: this.targetText().trim(),
          points,
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
          senderContext: this.composeSenderContext(),
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
      const [projectsRes, experiencesRes, hackathonsRes, academicRes] = await Promise.all([
        this.http.get<ApiResponse<any[]>>('/api/user-content/projects'),
        this.http.get<ApiResponse<any[]>>('/api/user-content/experiences'),
        this.http.get<ApiResponse<any[]>>('/api/user-content/hackathons'),
        this.http.get<ApiResponse<any[]>>('/api/user-content/academicactivities'),
      ]);

      const groups: CareerGroup[] = [];

      // Projects
      const projItems: CareerItem[] = (projectsRes.data ?? []).map((p: any) => {
        let tech = '';
        if (p.technologiesJson) {
          try {
            const parsed = JSON.parse(p.technologiesJson);
            tech = Array.isArray(parsed) ? parsed.join(', ') : String(parsed);
          } catch { tech = String(p.technologiesJson); }
        }
        const desc = p.description || '';
        const role = p.role ? ` as ${p.role}` : '';
        const text = [`Project: "${p.title}"${role}`, desc, tech ? `Tech: ${tech}` : '']
          .filter(Boolean).join('\n');
        return { key: `project:${p.id}`, label: p.title, text, source: 'project' as CareerSource };
      });
      if (projItems.length) groups.push({ label: 'Projects', source: 'project', items: projItems });

      // Experiences
      const expItems: CareerItem[] = (experiencesRes.data ?? []).map((e: any) => {
        const company = e.company ? ` at ${e.company}` : '';
        const loc = e.location ? ` (${e.location})` : '';
        const desc = e.description || '';
        const text = [`Experience: ${e.title}${company}${loc}`, desc].filter(Boolean).join('\n');
        return { key: `experience:${e.id}`, label: `${e.title}${company}`, text, source: 'experience' as CareerSource };
      });
      if (expItems.length) groups.push({ label: 'Experiences', source: 'experience', items: expItems });

      // Hackathons
      const hackItems: CareerItem[] = (hackathonsRes.data ?? []).map((h: any) => {
        const org = h.organization ? ` (${h.organization})` : '';
        const result = h.result ? ` — ${h.result}` : '';
        const tech = h.technologies ? `\nTech: ${h.technologies}` : '';
        const desc = h.description || '';
        const text = [`Hackathon: "${h.name}"${org}${result}`, desc, tech].filter(Boolean).join('\n');
        return { key: `hackathon:${h.id}`, label: h.name, text, source: 'hackathon' as CareerSource };
      });
      if (hackItems.length) groups.push({ label: 'Hackathons', source: 'hackathon', items: hackItems });

      // Scholar Activities
      const acadItems: CareerItem[] = (academicRes.data ?? []).map((a: any) => {
        const org = a.organization ? ` (${a.organization})` : '';
        const cat = a.category ? ` [${a.category}]` : '';
        const result = a.result ? ` — ${a.result}` : '';
        const desc = a.description || '';
        const text = [`Activity: "${a.title}"${org}${cat}${result}`, desc].filter(Boolean).join('\n');
        return { key: `academic:${a.id}`, label: a.title, text, source: 'academicactivity' as CareerSource };
      });
      if (acadItems.length) groups.push({ label: 'Scholar Activities', source: 'academicactivity', items: acadItems });

      this.careerGroups.set(groups);
      this.careerLoaded.set(true);
    } catch {
      this.toast.error('Could not load your career content');
    } finally {
      this.careerLoading.set(false);
    }
  }
}