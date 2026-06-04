import { Component, OnInit, OnDestroy, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CrawlerService } from '@app/services/crawler.service';
import { SignalRService } from '@app/services/signalr.service';
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
  private readonly signalR = inject(SignalRService);
  private readonly cdr = inject(ChangeDetectorRef);

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

  ngOnInit(): void {
    this.loadHistory();
  }

  ngOnDestroy(): void {
    this.signalR.offJobArrived();
    this.signalR.offSearchFinished();
    if (this.currentSearchId) {
      this.signalR.leaveGroup(this.currentSearchId);
    }
    this.signalR.disconnect();
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

    try {
      const userId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
      const response = await this.crawlerService.triggerCrawl(userId, {
        keyword: this.keyword,
        location: this.location,
        resultLimit: this.resultLimit,
      });

      this.currentSearchId = response.searchId;
      this.searchStatus = 'running';

      await this.signalR.connect();
      await this.signalR.joinGroup(response.searchId);

      this.signalR.onJobArrived((job: any) => {
        this.jobs.push({
          jobId: job.jobId,
          title: job.title ?? 'Unknown',
          company: job.company ?? 'Unknown',
          location: job.location ?? '',
          source: job.source ?? '',
          jobUrl: job.jobUrl ?? '',
          confidence: job.confidence ?? 0,
        });
        this.totalJobsFound = this.jobs.length;
        this.cdr.detectChanges();
      });

      this.signalR.onSearchFinished((data: any) => {
        this.searchStatus = 'completed';
        this.loadHistory();
        this.cdr.detectChanges();
      });
    } catch (err: any) {
      this.error = err.message ?? 'Failed to trigger crawl';
    } finally {
      this.loading = false;
    }
  }

  private async loadHistory(): Promise<void> {
    try {
      const userId = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
      this.history = await this.crawlerService.getHistory(userId);
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
}
