export interface CrawlRequest {
  keyword: string;
  location: string;
  resultLimit: number;
}

export interface CrawlResponse {
  searchId: string;
  keyword: string;
  location: string;
  resultLimit: number;
}

export interface CrawlJob {
  jobId: string;
  title: string;
  company: string;
  location: string;
  source: string;
  jobUrl: string;
  confidence: number;
}

export interface CrawlHistoryItem {
  searchId: string;
  keyword: string;
  location: string;
  status: 'Pending' | 'Extracting' | 'Completed' | 'Failed';
  expectedCount: number;
  processedCount: number;
  createdAt: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
}
