import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { SearchOutput } from '@app/services/search-agent.service';

@Component({
  selector: 'app-search-agent-result',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './search-agent-result.component.html',
  styleUrl: './search-agent-result.component.scss'
})
export class SearchAgentResultComponent implements OnInit {
  private readonly router = inject(Router);

  result: SearchOutput | null = null;

  ngOnInit() {
    const nav = this.router.getCurrentNavigation();
    if (nav?.extras.state && nav.extras.state['searchResult']) {
      this.result = nav.extras.state['searchResult'];
    } else if (history.state && history.state['searchResult']) {
      this.result = history.state['searchResult'];
    } else {
      // If no result is found in state, redirect back to workspace
      this.router.navigate(['/agents-hub/search-agent']);
    }
  }

  get matchPercentage(): number {
    if (!this.result) return 0;
    return Math.round(this.result.match_score * 100);
  }
}
