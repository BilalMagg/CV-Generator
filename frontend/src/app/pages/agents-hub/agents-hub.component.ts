import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AgentCardComponent, Agent } from './agent-card/agent-card.component';

@Component({
  selector: 'app-agents-hub',
  standalone: true,
  imports: [CommonModule, AgentCardComponent, RouterLink],
  templateUrl: './agents-hub.component.html',
  styleUrl: './agents-hub.component.scss'
})
export class AgentsHubComponent {
  private searchAgent: Agent = {
    id: 'search-agent',
    name: 'Search Agent',
    role: 'Smart Application Search',
    image: '/agents/search-agent.png',
    background: 'linear-gradient(145deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)',
    status: 'active',
  };

  private templateAgent: Agent = {
    id: 'template-agent',
    name: 'Template Agent',
    role: 'CV & Resume Generator',
    image: '/agents/template-agent.png',
    background: 'linear-gradient(145deg, #1b1b2f 0%, #2d1b4e 50%, #4a1942 100%)',
    status: 'active',
  };

  cards: { agent: Agent; size: 'wide' | 'tall' | 'small' | 'wide-small' }[] = [
    { size: 'tall',       agent: this.searchAgent },
    { size: 'tall',       agent: this.templateAgent },
    { size: 'wide',       agent: this.searchAgent },
    { size: 'wide-small', agent: this.templateAgent },
    { size: 'small',      agent: this.searchAgent },
    { size: 'small',      agent: this.templateAgent },
    { size: 'small',      agent: this.searchAgent },
  ];

  get uniqueAgents(): Agent[] {
    const seen = new Set<string>();
    return this.cards.filter(c => {
      if (seen.has(c.agent.id)) return false;
      seen.add(c.agent.id);
      return true;
    }).map(c => c.agent);
  }

  get totalAgents(): number {
    return this.uniqueAgents.length;
  }

  get activeAgents(): number {
    return this.uniqueAgents.filter(a => a.status === 'active').length;
  }

  get idleAgents(): number {
    return this.uniqueAgents.filter(a => a.status === 'idle').length;
  }

  get inactiveAgents(): number {
    return this.uniqueAgents.filter(a => a.status === 'inactive').length;
  }
}
