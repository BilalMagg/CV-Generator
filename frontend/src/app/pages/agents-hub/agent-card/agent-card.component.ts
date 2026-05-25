import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface Agent {
  id: string;
  name: string;
  role: string;
  image: string;
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
}
