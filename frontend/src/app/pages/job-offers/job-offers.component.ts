import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { CrawlerService } from '@app/services/crawler.service';
import { JobOfferSummary } from '@app/models/crawler.types';
import { RefreshButtonComponent } from '@app/shared/components/refresh-button/refresh-button.component';

@Component({
  selector: 'app-job-offers',
  standalone: true,
  imports: [CommonModule, RefreshButtonComponent],
  templateUrl: './job-offers.component.html',
  styleUrl: './job-offers.component.scss'
})
export class JobOffersComponent implements OnInit {
  private readonly crawlerService = inject(CrawlerService);
  private readonly router = inject(Router);

  // Private signals (internal state)
  private jobsSignal = signal<JobOfferSummary[]>([]);
  private loadingSignal = signal(true);
  private errorSignal = signal('');
  private totalSignal = signal(0);
  
  page = signal(1);
  pageSize = signal(50);

  // Public computed signals (for template use)
  jobs = computed(() => this.jobsSignal());
  loading = computed(() => this.loadingSignal());
  error = computed(() => this.errorSignal());
  total = computed(() => this.totalSignal());
  
  refreshing = signal(false);

  // Derived computed signals
  hasJobs = computed(() => this.jobsSignal().length > 0);
  isEmpty = computed(() => !this.loadingSignal() && !this.errorSignal() && this.jobsSignal().length === 0);
  showError = computed(() => !this.loadingSignal() && this.errorSignal());
  totalPages = computed(() => Math.ceil(this.totalSignal() / this.pageSize()));

  ngOnInit(): void {
    this.loadJobs();
  }

  async loadJobs(): Promise<void> {
    try {
      this.loadingSignal.set(true);
      this.errorSignal.set('');
      
      const result = await this.crawlerService.getAllJobs(this.page(), this.pageSize());
      console.log(result);
      
      this.jobsSignal.set(result.items);
      this.totalSignal.set(result.total);
    } catch (err: any) {
      this.errorSignal.set(err.message ?? 'Failed to load jobs');
      console.error('Error loading jobs:', err);
    } finally {
      this.loadingSignal.set(false);
      this.refreshing.set(false);
    }
  }

  onRefresh() { this.refreshing.set(true); this.loadJobs(); }

  viewJob(jobId: string): void {
    this.router.navigate(['/agents-hub/job-crawler/result', jobId]);
  }

  statusClass(status: string): string {
    switch (status) {
      case 'OPEN': return 'status-open';
      case 'DRAFT': return 'status-draft';
      case 'CLOSED': return 'status-closed';
      case 'ARCHIVED': return 'status-archived';
      default: return '';
    }
  }

  // Pagination methods
  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.page.update(p => p + 1);
      this.loadJobs();
    }
  }

  previousPage(): void {
    if (this.page() > 1) {
      this.page.update(p => p - 1);
      this.loadJobs();
    }
  }

  goToPage(pageNum: number): void {
    if (pageNum >= 1 && pageNum <= this.totalPages()) {
      this.page.set(pageNum);
      this.loadJobs();
    }
  }

  applyTo(job: JobOfferSummary): void {
    const qp: Record<string, string> = {};
    if (job.enterpriseName) qp['companyName'] = job.enterpriseName;
    if (job.jobRole) qp['positionTitle'] = job.jobRole;
    this.router.navigate(['/applications/apply'], { queryParams: qp });
  }
}