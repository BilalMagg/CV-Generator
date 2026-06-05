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

export interface CrawlPollResponse {
  searchId: string;
  status: string;
  keyword: string;
  location: string | null;
  expectedCount: number;
  processedCount: number;
  jobs: CrawlJob[];
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
}

export interface JobOfferSummary {
  id: string;
  userId: string;
  enterpriseName: string;
  jobRole: string;
  location: string | null;
  status: string;
  createdAt: string;
}

export interface JobOfferList {
  items: JobOfferSummary[];
  total: number;
  page: number;
  pageSize: number;
}

export interface JobSkillDetail {
  id: string;
  name: string;
  type: string;
  isMandatory: boolean;
}

export interface JobResponsibilityDetail {
  id: string;
  description: string;
}

export interface JobBenefitDetail {
  id: string;
  description: string;
}

export interface JobOfferDetail {
  id: string;
  userId: string;
  enterpriseName: string;
  enterpriseDescription: string | null;
  jobRole: string;
  rawDescription: string;
  requiredExperienceYears: number | null;
  seniorityLevel: string | null;
  employmentType: string | null;
  location: string | null;
  locationType: string | null;
  educationRequirements: string | null;
  sourceUrl: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
  skills: JobSkillDetail[];
  responsibilities: JobResponsibilityDetail[];
  benefits: JobBenefitDetail[];
}
