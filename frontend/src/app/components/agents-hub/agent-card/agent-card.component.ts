import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface Agent {
  id: string;
  name: string;
  role: string;
  background: string;
  status: 'active' | 'idle' | 'inactive';
}

@Component({
  selector: 'app-agent-card',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './agent-card.component.html',
  styleUrl: './agent-card.component.scss'
})
export class AgentCardComponent {
  @Input({ required: true }) agent!: Agent;
  @Input({ required: true }) size!: string;

  private readonly workspaceRoutes: Record<string, string> = {
    'job-extractor': '/agents-hub/job-extractor',
    'search-agent': '/agents-hub/search-agent',
    'template-agent': '/agents-hub/template-agent',
    'job-crawler': '/agents-hub/job-crawler',
  };

  private sizeMap: Record<string, string> = {
    'tall': 'tall',
    'wide': 'wide',
    'wide-small': 'small',
    'small': 'square',
  };

  get imagePath(): string {
    const file = this.sizeMap[this.size] || 'tall';
    return `/agents/${this.agent.id}/${file}.png`;
  }

  get workspaceRoute(): string | null {
    return this.workspaceRoutes[this.agent.id] ?? null;
  }
}
