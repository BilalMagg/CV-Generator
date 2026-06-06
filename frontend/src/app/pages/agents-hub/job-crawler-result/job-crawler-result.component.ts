import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { CrawlerService } from '@app/services/crawler.service';
import { JobOfferDetail } from '@app/models/crawler.types';

@Component({
  selector: 'app-job-crawler-result',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './job-crawler-result.component.html',
  styleUrl: './job-crawler-result.component.scss'
})
export class JobCrawlerResultComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly crawlerService = inject(CrawlerService);

  // Private signals (internal state)
  private jobSignal = signal<JobOfferDetail | null>(null);
  private loadingSignal = signal(true);
  private errorSignal = signal('');

  // Public computed signals (for template use)
  job = computed(() => this.jobSignal());
  loading = computed(() => this.loadingSignal());
  error = computed(() => this.errorSignal());

  // Derived computed signals - Fixed with null coalescing
  hasSkills = computed(() => (this.jobSignal()?.skills?.length ?? 0) > 0);
  hasResponsibilities = computed(() => (this.jobSignal()?.responsibilities?.length ?? 0) > 0);
  hasBenefits = computed(() => (this.jobSignal()?.benefits?.length ?? 0) > 0);
  hasEnterpriseDescription = computed(() => !!this.jobSignal()?.enterpriseDescription);
  hasRawDescription = computed(() => !!this.jobSignal()?.rawDescription);
  hasSourceUrl = computed(() => !!this.jobSignal()?.sourceUrl);
  
  // Computed for location display
  locationDisplay = computed(() => {
    const job = this.jobSignal();
    if (!job?.location) return null;
    return job.locationType ? `${job.location} (${job.locationType})` : job.location;
  });

  ngOnInit(): void {
    const jobId = this.route.snapshot.paramMap.get('id');
    if (jobId) {
      this.loadJob(jobId);
    } else {
      this.errorSignal.set('No job ID provided');
      this.loadingSignal.set(false);
    }
  }

  private async loadJob(jobId: string): Promise<void> {
    try {
      this.loadingSignal.set(true);
      this.errorSignal.set('');
      const job = await this.crawlerService.getJobDetail(jobId);
      this.jobSignal.set(job);
    } catch (err: any) {
      this.errorSignal.set(err.message ?? 'Failed to load job details');
      console.error('Error loading job:', err);
    } finally {
      this.loadingSignal.set(false);
    }
  }
}