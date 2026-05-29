import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

interface WizardCardOption {
  id: string;
  icon: string;
  label: string;
  description: string;
}

interface WizardFormField {
  key: string;
  label: string;
  type: 'text' | 'textarea' | 'select';
  placeholder?: string;
  options?: { value: string; label: string }[];
}

interface WizardToggle {
  key: string;
  label: string;
  defaultOn: boolean;
}

interface WizardStep {
  label: string;
  title: string;
  subtitle: string;
  cards?: WizardCardOption[];
  fields?: WizardFormField[];
  toggles?: WizardToggle[];
  isLaunch?: boolean;
}

interface WizardConfig {
  agentName: string;
  steps: WizardStep[];
  summaryLabel: (key: string) => string;
}

@Component({
  selector: 'app-agent-guide',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './agent-guide.component.html',
  styleUrl: './agent-guide.component.scss'
})
export class AgentGuideComponent implements OnInit {
  private route = inject(ActivatedRoute);

  agentName = '';
  currentStep = 0;
  selectedCard: Record<number, string> = {};
  formData: Record<string, string> = {};
  toggleStates: Record<string, boolean> = {};
  steps: WizardStep[] = [];

  private configs: Record<string, WizardConfig> = {
    'search-agent': {
      agentName: 'Search Agent',
      steps: [
        {
          label: 'Start',
          title: 'Launch from an agent template',
          subtitle: 'Choose from our most popular starting points to bring your agent online faster. Whether you\'re building from scratch, or connecting to live data, these templates give you a ready-made foundation.',
          cards: [
            { id: 'semantic', icon: '🔍', label: 'Semantic Search Agent', description: 'Full LLM-powered semantic search over your applications' },
            { id: 'keyword', icon: '📋', label: 'Keyword Search Agent', description: 'Classic keyword matching with smart ranking' },
            { id: 'hybrid', icon: '🔄', label: 'Hybrid Search Agent', description: 'Combines semantic + keyword for best results' },
            { id: 'rag', icon: '🗂️', label: 'RAG Search Agent', description: 'Connect to your own data sources for enriched search' },
          ],
        },
        {
          label: 'Agent type',
          title: 'Choose your agent type',
          subtitle: 'Select the primary capability that best describes what your agent will do.',
          cards: [
            { id: 'conversational', icon: '💬', label: 'Conversational', description: 'Multi-turn search with natural language' },
            { id: 'batch', icon: '⚡', label: 'Batch processor', description: 'Search through large sets of applications at once' },
            { id: 'realtime', icon: '🔔', label: 'Real-time monitor', description: 'Watches for new matching applications' },
            { id: 'report', icon: '📊', label: 'Report generator', description: 'Generates search summaries and insights' },
          ],
        },
        {
          label: 'Details',
          title: 'Agent details',
          subtitle: 'Give your agent a name, description, and personality.',
          fields: [
            { key: 'name', label: 'Agent name', type: 'text', placeholder: 'e.g. Search Assistant, Job Scout…' },
            { key: 'description', label: 'Description', type: 'textarea', placeholder: 'What does this agent do? Who is it for?' },
            { key: 'tone', label: 'Response tone', type: 'select', options: [
              { value: 'professional', label: 'Professional & concise' },
              { value: 'friendly', label: 'Friendly & casual' },
              { value: 'technical', label: 'Technical & precise' },
              { value: 'supportive', label: 'Empathetic & supportive' },
            ]},
          ],
        },
        {
          label: 'Discovery',
          title: 'Discovery settings',
          subtitle: 'Control how your agent is found and who can interact with it.',
          toggles: [
            { key: 'public', label: 'List in public directory', defaultOn: true },
            { key: 'share', label: 'Allow direct sharing link', defaultOn: true },
            { key: 'auth', label: 'Require authentication', defaultOn: false },
            { key: 'analytics', label: 'Enable analytics', defaultOn: true },
            { key: 'feedback', label: 'Allow user feedback', defaultOn: true },
          ],
        },
        {
          label: 'Launch',
          title: 'Your agent is ready to launch',
          subtitle: 'Everything looks good. Hit launch to bring your agent online — you can tweak settings any time.',
          isLaunch: true,
        },
      ],
      summaryLabel: (key: string) => {
        const map: Record<string, string> = {
          template: 'Template', type: 'Type', name: 'Name',
          tone: 'Tone', public: 'Discovery', analytics: 'Analytics',
        };
        return map[key] ?? key;
      },
    },
    'template-agent': {
      agentName: 'Template Agent',
      steps: [
        {
          label: 'Start',
          title: 'Launch from an agent template',
          subtitle: 'Choose from our most popular starting points to bring your agent online faster. Whether you\'re building from scratch, or connecting to live data, these templates give you a ready-made foundation.',
          cards: [
            { id: 'cv-standard', icon: '📄', label: 'Standard CV Template', description: 'Professional CV generator with classic layout' },
            { id: 'cv-creative', icon: '🎨', label: 'Creative CV Template', description: 'Modern design for creative industries' },
            { id: 'cover-letter', icon: '✉️', label: 'Cover Letter Template', description: 'Auto-generates tailored cover letters' },
            { id: 'portfolio', icon: '🖼️', label: 'Portfolio Template', description: 'Showcase projects and achievements' },
          ],
        },
        {
          label: 'Agent type',
          title: 'Choose your agent type',
          subtitle: 'Select the primary capability that best describes what your agent will do.',
          cards: [
            { id: 'auto', icon: '⚡', label: 'Auto-generate', description: 'Fully automatic CV generation from your profile' },
            { id: 'guided', icon: '📝', label: 'Guided builder', description: 'Step-by-step CV creation with suggestions' },
            { id: 'customizer', icon: '🎨', label: 'Template customizer', description: 'Tweak existing templates to your needs' },
            { id: 'batch', icon: '📦', label: 'Batch generator', description: 'Generate multiple CV variants at once' },
          ],
        },
        {
          label: 'Details',
          title: 'Agent details',
          subtitle: 'Give your agent a name, description, and personality.',
          fields: [
            { key: 'name', label: 'Agent name', type: 'text', placeholder: 'e.g. CV Assistant, Resume Builder…' },
            { key: 'description', label: 'Description', type: 'textarea', placeholder: 'What does this agent do? Who is it for?' },
            { key: 'style', label: 'Default style', type: 'select', options: [
              { value: 'modern', label: 'Modern & clean' },
              { value: 'classic', label: 'Classic & traditional' },
              { value: 'minimal', label: 'Minimal & elegant' },
              { value: 'bold', label: 'Bold & creative' },
            ]},
          ],
        },
        {
          label: 'Discovery',
          title: 'Discovery settings',
          subtitle: 'Control how your agent is found and who can interact with it.',
          toggles: [
            { key: 'public', label: 'List in public directory', defaultOn: true },
            { key: 'share', label: 'Allow direct sharing link', defaultOn: true },
            { key: 'auth', label: 'Require authentication', defaultOn: false },
            { key: 'analytics', label: 'Enable analytics', defaultOn: true },
            { key: 'feedback', label: 'Allow user feedback', defaultOn: true },
          ],
        },
        {
          label: 'Launch',
          title: 'Your agent is ready to launch',
          subtitle: 'Everything looks good. Hit launch to bring your agent online — you can tweak settings any time.',
          isLaunch: true,
        },
      ],
      summaryLabel: (key: string) => {
        const map: Record<string, string> = {
          template: 'Template', type: 'Type', name: 'Name',
          style: 'Style', public: 'Discovery', analytics: 'Analytics',
        };
        return map[key] ?? key;
      },
    },
  };

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    const config = this.configs[id];
    if (config) {
      this.agentName = config.agentName;
      this.steps = config.steps;
      this.initToggles(config);
    }
  }

  private initToggles(config: WizardConfig) {
    for (const step of config.steps) {
      if (step.toggles) {
        for (const t of step.toggles) {
          this.toggleStates[t.key] = t.defaultOn;
        }
      }
    }
  }

  get currentStepConfig() {
    return this.steps[this.currentStep];
  }

  get isFirstStep() {
    return this.currentStep === 0;
  }

  get isLastStep() {
    return this.currentStep === this.steps.length - 1;
  }

  selectCard(id: string) {
    this.selectedCard[this.currentStep] = id;
  }

  isCardSelected(id: string) {
    return this.selectedCard[this.currentStep] === id;
  }

  get hasSelection() {
    const s = this.currentStepConfig;
    if (s.cards) return !!this.selectedCard[this.currentStep];
    if (s.fields) return true;
    if (s.toggles) return true;
    if (s.isLaunch) return true;
    return false;
  }

  next() {
    if (this.currentStep < this.steps.length - 1) {
      this.currentStep++;
    }
  }

  prev() {
    if (this.currentStep > 0) {
      this.currentStep--;
    }
  }

  goToStep(i: number) {
    if (i <= this.currentStep) {
      this.currentStep = i;
    }
  }

  launch() {
    // placeholder — will wire up later
  }

  summaryItems(): { label: string; value: string }[] {
    const config = this.configs[this.route.snapshot.paramMap.get('id') ?? ''];
    if (!config) return [];
    const items: { label: string; value: string }[] = [];
    for (let i = 0; i < this.steps.length - 1; i++) {
      const step = this.steps[i];
      if (step.cards && this.selectedCard[i]) {
        const card = step.cards.find(c => c.id === this.selectedCard[i]);
        items.push({ label: config.summaryLabel('template'), value: card?.label ?? this.selectedCard[i] });
      }
      if (step.fields) {
        for (const f of step.fields) {
          if (this.formData[f.key]) {
            items.push({ label: config.summaryLabel(f.key), value: this.formData[f.key] });
          }
        }
      }
      if (step.toggles) {
        const onToggles = step.toggles.filter(t => this.toggleStates[t.key]);
        for (const t of onToggles) {
          items.push({ label: config.summaryLabel(t.key), value: 'Enabled' });
        }
      }
    }
    return items;
  }
}
