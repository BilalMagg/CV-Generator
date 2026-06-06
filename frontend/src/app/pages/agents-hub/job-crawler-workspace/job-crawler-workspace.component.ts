import { Component, OnInit, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { CrawlerService } from '@app/services/crawler.service';
import { CrawlJob, CrawlHistoryItem } from '@app/models/crawler.types';

@Component({
  selector: 'app-job-crawler-workspace',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './job-crawler-workspace.component.html',
  styleUrl: './job-crawler-workspace.component.scss'
})
export class JobCrawlerWorkspaceComponent implements OnInit, OnDestroy {
  private readonly crawlerService = inject(CrawlerService);
  private readonly router = inject(Router);

  keyword = '';
  location = '';
  resultLimit = 20;
  loading = false;
  error = '';
  currentSearchId: string | null = null;
  searchStatus: 'idle' | 'running' | 'completed' = 'idle';

  jobs: CrawlJob[] = [];
  history: CrawlHistoryItem[] = [];
  totalCrawls = 0;
  totalJobsFound = 0;

  showWarning = false;
  crawlTimeout = false;
  private pollStartTime = 0;
  private readonly WARNING_MS = 120_000;
  private readonly TIMEOUT_MS = 300_000;

  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.loadHistory();
  }

  ngOnDestroy(): void {
    this.clearPolling();
  }

  get canTrigger(): boolean {
    return this.keyword.trim().length > 0 && this.location.trim().length > 0 && !this.loading;
  }

  async triggerCrawl(): Promise<void> {
    if (!this.canTrigger) return;
    this.loading = true;
    this.error = '';
    this.jobs = [];
    this.currentSearchId = null;
    this.searchStatus = 'idle';
    this.clearPolling();

    try {
      const response = await this.crawlerService.triggerCrawl({
        keyword: this.keyword,
        location: this.location,
        resultLimit: this.resultLimit,
      });

      this.currentSearchId = response.searchId;
      this.searchStatus = 'running';
      this.showWarning = false;
      this.crawlTimeout = false;
      this.pollStartTime = Date.now();

      this.pollTimer = setInterval(async () => {
        try {
          const elapsed = Date.now() - this.pollStartTime;

          if (elapsed >= this.TIMEOUT_MS) {
            this.clearPolling();
            this.crawlTimeout = true;
            this.showWarning = false;
            this.searchStatus = 'completed';
            this.loadHistory();
            return;
          }

          if (elapsed >= this.WARNING_MS) {
            this.showWarning = true;
          }

          const result = await this.crawlerService.pollCrawlResults(this.currentSearchId!);
          this.jobs = result.jobs;
          this.totalJobsFound = this.jobs.length;
          this.searchStatus = result.status === 'Completed' ? 'completed' : 'running';
          if (result.status === 'Completed' || result.status === 'Failed') {
            this.clearPolling();
            this.showWarning = false;
            this.loadHistory();
          }
        } catch {
          this.clearPolling();
          this.error = 'Polling failed';
        }
      }, 2000);

    } catch (err: any) {
      this.error = err.message ?? 'Failed to trigger crawl';
    } finally {
      this.loading = false;
    }
  }

  private clearPolling(): void {
    if (this.pollTimer) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private async loadHistory(): Promise<void> {
    try {
      this.history = await this.crawlerService.getHistory();
      console.log(this.history)
      this.totalCrawls = this.history.length;
    } catch {
      this.history = [];
    }
  }

  formatDate(dateStr: string): string {
    const d = new Date(dateStr);
    const now = new Date();
    const diff = now.getTime() - d.getTime();
    const mins = Math.floor(diff / 60000);
    if (mins < 60) return `${mins}m ago`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours}h ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days}d ago`;
    return d.toLocaleDateString();
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Completed': return 'status-done';
      case 'Extracting': return 'status-progress';
      case 'Pending': return 'status-pending';
      case 'Failed': return 'status-failed';
      default: return '';
    }
  }

  viewJob(jobId: string): void {
    this.router.navigate(['/agents-hub/job-crawler/result', jobId]);
  }
}
