import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AgentCardComponent, Agent } from './agent-card/agent-card.component';
import { AgentHealthService } from './agent-health.service';

@Component({
  selector: 'app-agents-hub',
  standalone: true,
  imports: [CommonModule, AgentCardComponent, RouterLink],
  templateUrl: './agents-hub.component.html',
  styleUrl: './agents-hub.component.scss'
})
export class AgentsHubComponent implements OnInit, OnDestroy {
  private readonly healthService = inject(AgentHealthService);
  private readonly cdr = inject(ChangeDetectorRef);
  private healthPollTimer: ReturnType<typeof setInterval> | null = null;

  private jobExtractorAgent: Agent = {
    id: 'job-extractor',
    name: 'Job Extractor',
    role: 'Job Description Parser',
    background: 'linear-gradient(145deg, #0c2340 0%, #1a3a5c 50%, #2d6a9f 100%)',
    status: 'active',
  };

  private searchAgent: Agent = {
    id: 'search-agent',
    name: 'Search Agent',
    role: 'Smart Application Search',
    background: 'linear-gradient(145deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%)',
    status: 'active',
  };

  private templateAgent: Agent = {
    id: 'template-agent',
    name: 'Template Agent',
    role: 'CV & Resume Generator',
    background: 'linear-gradient(145deg, #1b1b2f 0%, #2d1b4e 50%, #4a1942 100%)',
    status: 'active',
  };

  private cvOptimizerAgent: Agent = {
    id: 'cv-optimizer',
    name: 'CV Optimizer',
    role: 'Tailored CV Enhancer',
    background: 'linear-gradient(145deg, #0d2818 0%, #1a3c2a 50%, #2d6b4a 100%)',
    status: 'active',
  };

  private contactAgent: Agent = {
    id: 'contact-agent',
    name: 'Contact Agent',
    role: 'Application Delivery',
    background: 'linear-gradient(145deg, #2d0a28 0%, #4a154b 50%, #7b2d6b 100%)',
    status: 'active',
  };

  private allAgents: Agent[] = [
    this.jobExtractorAgent,
    this.searchAgent,
    this.templateAgent,
    this.cvOptimizerAgent,
    this.contactAgent,
  ];

  cards: { agent: Agent; size: 'wide' | 'tall' | 'small' | 'wide-small' }[] = [
    { size: 'tall',       agent: this.searchAgent },
    { size: 'tall',       agent: this.templateAgent },
    { size: 'wide',       agent: this.searchAgent },
    { size: 'wide-small', agent: this.templateAgent },
    { size: 'small',      agent: this.jobExtractorAgent },
    { size: 'small',      agent: this.cvOptimizerAgent },
    { size: 'small',      agent: this.contactAgent },
    { size: 'small',      agent: this.searchAgent },
  ];

  get uniqueAgents(): Agent[] {
    return this.allAgents;
  }

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

  ngOnInit(): void {
    this.pollHealth();
    this.healthPollTimer = setInterval(() => this.pollHealth(), 30000);
  }

  ngOnDestroy(): void {
    if (this.healthPollTimer) {
      clearInterval(this.healthPollTimer);
    }
  }

  private readonly agentKeyMap: Record<string, string> = {
    'job-extractor': 'jobExtractor',
    'search-agent': 'searchAgent',
    'template-agent': 'templateAgent',
    'cv-optimizer': 'cvOptimizer',
    'contact-agent': 'contactAgent',
  };

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
