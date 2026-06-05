import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { CrawlRequest, CrawlResponse, CrawlHistoryItem, CrawlPollResponse, ApiResponse, JobOfferList, JobOfferDetail } from '@app/models/crawler.types';

@Injectable({
  providedIn: 'root'
})
export class CrawlerService {
  private readonly http = inject(HttpService);

  async triggerCrawl(request: CrawlRequest): Promise<CrawlResponse> {
    const response = await this.http.post<ApiResponse<CrawlResponse>>('/api/workflows/crawl', {
      keyword: request.keyword,
      location: request.location,
      resultLimit: request.resultLimit,
    });
    if (!response.success || !response.data) throw new Error(response.message ?? 'Failed to trigger crawl');
    return response.data;
  }

  async pollCrawlResults(searchId: string): Promise<CrawlPollResponse> {
    const response = await this.http.get<ApiResponse<CrawlPollResponse>>(`/api/job-offers/crawls/${searchId}`);
    if (!response.success || !response.data) throw new Error(response.message ?? 'Failed to poll crawl');
    return response.data;
  }

  async getHistory(): Promise<CrawlHistoryItem[]> {
    const response = await this.http.get<ApiResponse<CrawlHistoryItem[]>>('/api/job-offers/crawls');
    return response.data ?? [];
  }

  async getJobDetail(jobId: string): Promise<JobOfferDetail> {
    const response = await this.http.get<ApiResponse<JobOfferDetail>>(`/api/job-offers/${jobId}`);
    if (!response.success || !response.data) throw new Error(response.message ?? 'Failed to load job');
    return response.data;
  }

  async getAllJobs(page = 1, pageSize = 50): Promise<JobOfferList> {
    const response = await this.http.get<ApiResponse<JobOfferList>>(`/api/job-offers/all?page=${page}&pageSize=${pageSize}`);
    if (!response.success || !response.data) throw new Error(response.message ?? 'Failed to load jobs');
    return response.data;
  }
}
