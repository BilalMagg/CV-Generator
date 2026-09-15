import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpService } from '@app/services/http.service';
import { LinkedInToolService } from '@app/services/linkedin-tool.service';
import { ToastService } from '@app/services/toast.service';
import { ApiResponse } from '@app/models/application.model';
import {
  EmojifyDensity,
  LinkedInLength,
  LinkedInRequest,
  LinkedInTone,
  LinkedInToolType,
  LinkedInVariant,
  SavedToolContent,
  ToolTab,
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

  tool = signal<ToolTab>('post');

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

  // Emojies
  emojiText = signal('');
  emojiPreset = signal('');
  emojiHint = signal('');
  emojiDensity = signal<EmojifyDensity>('medium');

  // Run state
  loading = signal(false);
  error = signal('');
  results = signal<LinkedInVariant[]>([]);
  generatedTool = signal<ToolTab>('post');

  // Adjust (rework a liked variant)
  adjustOpen = signal(false);
  adjustBaseText = signal('');
  adjustInstruction = signal('');
  adjustVariants = signal(2);

  // Saved library
  savedItems = signal<SavedToolContent[]>([]);
  savedLoading = signal(false);
  savedLoaded = signal(false);
  savedError = signal('');
  /** Saved item currently being reworked/updated/reverted/deleted. */
  applyingTo = signal<string | null>(null);
  /** Saved item opened in the adjust modal (null = adjusting a fresh variant). */
  adjustingItem = signal<SavedToolContent | null>(null);
  /** Index of the result card whose Save is in flight. */
  savingId = signal<number | null>(null);

  // Career picker
  careerGroups = signal<CareerGroup[]>([]);
  careerLoading = signal(false);
  careerLoaded = signal(false);
  careerPickerOpen = signal(false);
  selectedKeys = signal<Set<string>>(new Set());

  // Entity modal
  activeGroup = signal<CareerGroup | null>(null);
  draftKeys = signal<Set<string>>(new Set());

  countFor(group: CareerGroup): number {
    const set = this.selectedKeys();
    return group.items.filter((i) => set.has(i.key)).length;
  }

  hasAnySelection(): boolean {
    return this.careerGroups().some((g) => this.countFor(g) > 0);
  }

  hasAnyItems(): boolean {
    return this.careerGroups().some((g) => g.items.length > 0);
  }

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

  readonly tabs: { value: ToolTab; label: string }[] = [
    { value: 'post', label: 'Post' },
    { value: 'comment', label: 'Comment' },
    { value: 'message', label: 'Message' },
    { value: 'emojify', label: 'Emojies' },
    { value: 'saved', label: 'Saved' },
  ];

  readonly emojiPresets: Option[] = [
    { value: '', label: 'My own instruction' },
    { value: 'one emoji per paragraph, placed at the most impactful point', label: 'One per paragraph' },
    { value: 'only subtle, professional emojis — keep it corporate', label: 'Professional & subtle' },
    { value: 'uplifting and enthusiastic — make it feel celebratory', label: 'Celebratory' },
    { value: 'playful and light, like a chat with friends', label: 'Fun & light' },
    { value: 'very minimal — a single emoji at the very end', label: 'Minimal' },
  ];
  readonly densities: Option[] = [
    { value: 'low', label: 'Low' },
    { value: 'medium', label: 'Medium' },
    { value: 'high', label: 'High' },
  ];

  splitLines(text: string): string[] {
    return text.split(/\r?\n/).map((s) => s.trim()).filter(Boolean);
  }

  genLabel(plural: boolean): string {
    const g = this.generatedTool();
    if (g === 'emojify') return plural ? 'texts' : 'text';
    const base = g === 'post' ? 'post' : g === 'comment' ? 'comment' : 'message';
    return plural ? base + 's' : base;
  }

  // --- Career picker ---

  toggleCareerPicker(): void {
    const next = !this.careerPickerOpen();
    this.careerPickerOpen.set(next);
    if (next && !this.careerLoaded()) this.loadCareer();
  }

  // --- Entity modal ---

  openEntityModal(group: CareerGroup): void {
    this.activeGroup.set(group);
    this.draftKeys.set(new Set([...this.selectedKeys()].filter((k) => k.startsWith(`${group.source}:`))));
  }

  closeEntityModal(): void {
    this.activeGroup.set(null);
  }

  isDraftSelected(key: string): boolean {
    return this.draftKeys().has(key);
  }

  toggleDraft(item: CareerItem): void {
    const set = new Set(this.draftKeys());
    if (set.has(item.key)) {
      set.delete(item.key);
    } else {
      set.add(item.key);
    }
    this.draftKeys.set(set);
  }

  selectAllDraft(): void {
    const group = this.activeGroup();
    if (!group) return;
    const set = new Set(this.draftKeys());
    const allSelected = group.items.every((i) => set.has(i.key));
    for (const item of group.items) {
      if (allSelected) {
        set.delete(item.key);
      } else {
        set.add(item.key);
      }
    }
    this.draftKeys.set(set);
  }

  isDraftAllSelected(): boolean {
    const group = this.activeGroup();
    if (!group || group.items.length === 0) return false;
    return group.items.every((i) => this.draftKeys().has(i.key));
  }

  isDraftPartiallySelected(): boolean {
    const group = this.activeGroup();
    if (!group) return false;
    const set = this.draftKeys();
    return !group.items.every((i) => set.has(i.key)) && group.items.some((i) => set.has(i.key));
  }

  confirmDraft(): void {
    const group = this.activeGroup();
    if (!group) return;
    const set = new Set(this.selectedKeys());
    for (const item of group.items) {
      if (this.draftKeys().has(item.key)) {
        set.add(item.key);
      } else {
        set.delete(item.key);
      }
    }
    this.selectedKeys.set(set);
    this.closeEntityModal();
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

  private lastRequest: LinkedInRequest | null = null;

  async generate(): Promise<void> {
    if (this.tool() === 'saved') {
      await this.loadSaved();
      return;
    }
    if (this.tool() === 'emojify') {
      await this.runEmojify();
      return;
    }
    let request: LinkedInRequest;
    try {
      request = this.buildRequest();
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Please fill the required fields');
      return;
    }
    await this.runGenerate(request);
  }

  private async runEmojify(): Promise<void> {
    const text = this.emojiText().trim();
    if (!text) {
      this.toast.error('Paste the text you want to add emojis to');
      return;
    }
    const hintParts = [this.emojiPreset() ? 'Preset: ' + this.emojiPreset() : '', this.emojiHint().trim()]
      .filter(Boolean)
      .join('\n');

    this.loading.set(true);
    this.error.set('');
    this.generatedTool.set('emojify');
    try {
      const res = await this.service.emojify({
        text,
        hint: hintParts,
        density: this.emojiDensity(),
        language: this.language(),
        variants: this.variants(),
      });
      const variants = (res.data?.variants ?? []).map((v) => ({ title: '', text: v.text, hashtags: '' }));
      if (variants.length === 0) {
        this.error.set('The assistant returned nothing usable — try again or rephrase the hint.');
      } else {
        this.results.set(variants);
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Generation failed. Check that the AI service is running.');
    } finally {
      this.loading.set(false);
    }
  }

  private async runGenerate(request: LinkedInRequest): Promise<void> {
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
        this.lastRequest = request;
      }
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Generation failed. Check that the AI service is running.');
    } finally {
      this.loading.set(false);
    }
  }

  // --- Adjust (rework a liked variant) ---

  openAdjust(variant: LinkedInVariant): void {
    const parts = [variant.title, variant.text, variant.hashtags].filter(Boolean);
    this.adjustBaseText.set(parts.join('\n\n'));
    this.adjustInstruction.set('');
    this.adjustVariants.set(this.variants());
    this.adjustingItem.set(null);
    this.adjustOpen.set(true);
  }

  openAdjustFromLibrary(item: SavedToolContent): void {
    this.adjustBaseText.set([item.title, item.text, item.hashtags].filter(Boolean).join('\n\n'));
    this.adjustInstruction.set('');
    this.adjustVariants.set(this.variants());
    this.adjustingItem.set(item);
    this.adjustOpen.set(true);
  }

  closeAdjust(): void {
    if (!this.loading()) {
      this.adjustOpen.set(false);
      if (!this.error()) this.adjustingItem.set(null);
    }
  }

  async submitAdjust(): Promise<void> {
    const baseText = this.adjustBaseText().trim();
    const adjustment = this.adjustInstruction().trim();
    if (!baseText) {
      this.toast.error('The draft to adjust is empty');
      return;
    }
    if (!adjustment) {
      this.toast.error('Describe what you want to change before generating');
      return;
    }

    let request: LinkedInRequest;
    const item = this.adjustingItem();
    if (item) {
      // Adjusting a saved library item: (re)generate against that item's tool
      // with the shared tone/length settings — no source-form fields needed.
      request = {
        tool: (item.tool as LinkedInToolType),
        language: this.language(),
        tone: this.tone(),
        length: this.length(),
        variants: this.adjustVariants(),
        baseText,
        adjustment,
      };
    } else {
      try {
        request = this.buildRequest();
      } catch (e) {
        // Adjust mode relaxes the source-field requirement — fall back to the last
        // successful request so the tool/context still travels with the rework.
        if (this.lastRequest) {
          request = { ...this.lastRequest };
        } else {
          this.toast.error(e instanceof Error ? e.message : 'Please fill the required fields');
          return;
        }
      }
      request.variants = this.adjustVariants();
      request.baseText = baseText;
      request.adjustment = adjustment;
    }

    await this.runGenerate(request);
    if (!this.error()) {
      this.adjustOpen.set(false);
      if (item) {
        // Library rework: apply the first new variant back to the item so the
        // original stays recoverable via "Restore original".
        const first = this.results()[0];
        if (first) {
          await this.updateSavedFromVariant(item, first);
        }
        this.adjustingItem.set(null);
      }
    }
  }

  // --- Saved library ---

  toolLabel(tool: ToolTab): string {
    if (tool === 'emojify') return 'Emojies';
    if (tool === 'saved') return 'Saved';
    return tool;
  }

  async loadSaved(force = false): Promise<void> {
    if (this.savedLoaded() && !force) return;
    if (this.savedLoading()) return;
    this.savedLoading.set(true);
    this.savedError.set('');
    try {
      const res = await this.service.listSaved();
      this.savedItems.set(res.data ?? []);
      this.savedLoaded.set(true);
    } catch (e) {
      this.savedError.set(e instanceof Error ? e.message : 'Could not load your saved content.');
    } finally {
      this.savedLoading.set(false);
    }
  }

  async saveVariant(variant: LinkedInVariant): Promise<void> {
    const idx = this.results().indexOf(variant);
    if (idx >= 0) this.savingId.set(idx);
    try {
      await this.service.createSaved({
        tool: this.generatedTool(),
        title: variant.title || undefined,
        text: variant.text,
        hashtags: variant.hashtags || undefined,
      });
      this.toast.success('Saved to your library');
      if (this.savedLoaded()) await this.loadSaved(true);
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Could not save — try again');
    } finally {
      this.savingId.set(null);
    }
  }

  /** Put a reworked variant back into a library item (its OriginalText is preserved for revert). */
  async updateSavedFromVariant(item: SavedToolContent, variant: LinkedInVariant): Promise<void> {
    this.applyingTo.set(item.id);
    try {
      await this.service.updateSaved(item.id, {
        title: variant.title || item.title,
        text: variant.text,
        hashtags: variant.hashtags || item.hashtags,
      });
      this.toast.success('Saved item updated — you can still restore the original');
      await this.loadSaved(true);
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Could not update the saved item');
    } finally {
      this.applyingTo.set(null);
    }
  }

  async revertSaved(item: SavedToolContent): Promise<void> {
    this.applyingTo.set(item.id);
    try {
      await this.service.revertSaved(item.id);
      this.toast.success('Restored the original text');
      await this.loadSaved(true);
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Could not restore the original');
    } finally {
      this.applyingTo.set(null);
    }
  }

  async deleteSaved(item: SavedToolContent): Promise<void> {
    this.applyingTo.set(item.id);
    try {
      await this.service.deleteSaved(item.id);
      this.toast.success('Deleted');
      this.savedItems.set(this.savedItems().filter((i) => i.id !== item.id));
    } catch (e) {
      this.toast.error(e instanceof Error ? e.message : 'Could not delete — try again');
    } finally {
      this.applyingTo.set(null);
    }
  }

  relativeTime(iso: string): string {
    const d = new Date(iso).getTime();
    if (Number.isNaN(d)) return '';
    const diff = Date.now() - d;
    const mins = Math.floor(diff / 60000);
    if (mins < 1) return 'just now';
    if (mins < 60) return `${mins}m ago`;
    const hrs = Math.floor(mins / 60);
    if (hrs < 24) return `${hrs}h ago`;
    const days = Math.floor(hrs / 24);
    if (days < 7) return `${days}d ago`;
    return new Date(iso).toLocaleDateString();
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
      default: {
        throw new Error('Pick a LinkedIn tool to generate');
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
      groups.push({ label: 'Projects', source: 'project', items: projItems });

      // Experiences
      const expItems: CareerItem[] = (experiencesRes.data ?? []).map((e: any) => {
        const company = e.company ? ` at ${e.company}` : '';
        const loc = e.location ? ` (${e.location})` : '';
        const desc = e.description || '';
        const text = [`Experience: ${e.title}${company}${loc}`, desc].filter(Boolean).join('\n');
        return { key: `experience:${e.id}`, label: `${e.title}${company}`, text, source: 'experience' as CareerSource };
      });
      groups.push({ label: 'Experiences', source: 'experience', items: expItems });

      // Hackathons
      const hackItems: CareerItem[] = (hackathonsRes.data ?? []).map((h: any) => {
        const org = h.organization ? ` (${h.organization})` : '';
        const result = h.result ? ` — ${h.result}` : '';
        const tech = h.technologies ? `\nTech: ${h.technologies}` : '';
        const desc = h.description || '';
        const text = [`Hackathon: "${h.name}"${org}${result}`, desc, tech].filter(Boolean).join('\n');
        return { key: `hackathon:${h.id}`, label: h.name, text, source: 'hackathon' as CareerSource };
      });
      groups.push({ label: 'Hackathons', source: 'hackathon', items: hackItems });

      // Scholar Activities
      const acadItems: CareerItem[] = (academicRes.data ?? []).map((a: any) => {
        const org = a.organization ? ` (${a.organization})` : '';
        const cat = a.category ? ` [${a.category}]` : '';
        const result = a.result ? ` — ${a.result}` : '';
        const desc = a.description || '';
        const text = [`Activity: "${a.title}"${org}${cat}${result}`, desc].filter(Boolean).join('\n');
        return { key: `academic:${a.id}`, label: a.title, text, source: 'academicactivity' as CareerSource };
      });
      groups.push({ label: 'Scholar Activities', source: 'academicactivity', items: acadItems });

      this.careerGroups.set(groups);
      this.careerLoaded.set(true);
    } catch {
      this.toast.error('Could not load your career content');
    } finally {
      this.careerLoading.set(false);
    }
  }
}