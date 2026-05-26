import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AgentCardComponent, Agent } from './agent-card/agent-card.component';
import { AgentHealthService } from './agent-health.service';
import { AgentService } from './agent.service';

@Component({
  selector: 'app-agents-hub',
  standalone: true,
  imports: [CommonModule, AgentCardComponent, RouterLink],
  templateUrl: './agents-hub.component.html',
  styleUrl: './agents-hub.component.scss'
})
export class AgentsHubComponent implements OnInit, OnDestroy {
  private readonly agentService = inject(AgentService);
  private readonly healthService = inject(AgentHealthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private healthPollTimer: ReturnType<typeof setInterval> | null = null;

  allAgents: Agent[] = [];
  cards: { agent: Agent; size: 'wide' | 'tall' | 'small' | 'wide-small' }[] = [];

  get totalAgents(): number {
    return this.allAgents.length;
  }

  get activeAgents(): number {
    return this.allAgents.filter(a => a.status === 'active').length;
  }

  get idleAgents(): number {
    return this.allAgents.filter(a => a.status === 'idle').length;
  }

  get inactiveAgents(): number {
    return this.allAgents.filter(a => a.status === 'inactive').length;
  }

  readonly agentKeyMap: Record<string, string> = {
    'job-extractor': 'jobExtractor',
    'search-agent': 'searchAgent',
    'template-agent': 'templateAgent',
    'cv-optimizer': 'cvOptimizer',
    'contact-agent': 'contactAgent',
  };

  async ngOnInit(): Promise<void> {
    await this.loadAgents();
    this.pollHealth();
    this.healthPollTimer = setInterval(() => this.pollHealth(), 30000);
  }

  ngOnDestroy(): void {
    if (this.healthPollTimer) {
      clearInterval(this.healthPollTimer);
    }
  }

  private async loadAgents(): Promise<void> {
    try {
      const agents = await this.agentService.getAgents();
      this.allAgents = agents;
      this.buildCards(agents);
      this.cdr.detectChanges();
    } catch {
      // Fall back to empty state on network error
    }
  }

  private buildCards(agents: Agent[]): void {
    const sizes: ('wide' | 'tall' | 'small' | 'wide-small')[] = [
      'tall', 'tall', 'small', 'small', 'small',
    ];
    const result: { agent: Agent; size: 'wide' | 'tall' | 'small' | 'wide-small' }[] = [];

    for (let i = 0; i < agents.length; i++) {
      const size = sizes[i] ?? 'small';
      result.push({ agent: agents[i], size });
    }

    this.cards = result;
  }

  private async pollHealth(): Promise<void> {
    try {
      const healthMap = await this.healthService.checkHealth();
      for (const agent of this.allAgents) {
        const key = this.agentKeyMap[agent.id];
        const health = key ? healthMap[key] : undefined;
        if (health) {
          agent.status = health.healthy ? 'active' : 'inactive';
        }
      }
      this.cdr.detectChanges();
    } catch {
      // Keep existing statuses on network error
    }
  }
}
