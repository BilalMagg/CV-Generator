export type ApplicationStatus =
  | 'PENDING'
  | 'REVIEWED'
  | 'INTERVIEW'
  | 'ACCEPTED'
  | 'REJECTED'
  | 'CANCELLED';

export interface StatusHistoryDto {
  id: string;
  oldStatus?: string;
  newStatus: string;
  changedAt: string;
  changedBy?: string;
  comment?: string;
}

export interface ApplicationResponseDto {
  id: string;
  candidateId: string;
  cvVersionId?: string;
  jobOfferId?: string;
  companyName: string;
  positionTitle: string;
  offerSource?: string;
  status: ApplicationStatus;
  appliedAt: string;
  updatedAt: string;
  notes?: string;
  history?: StatusHistoryDto[];
}

export interface CreateApplicationDto {
  candidateId: string;
  cvVersionId?: string;
  jobOfferId?: string;
  companyName: string;
  positionTitle: string;
  offerSource?: string;
  notes?: string;
}

export interface UpdateStatusDto {
  status: ApplicationStatus;
  comment?: string;
}

export interface UpdateApplicationDto {
  companyName?: string;
  positionTitle?: string;
  offerSource?: string;
  notes?: string;
}

export interface ApplicationStatisticsDto {
  total: number;
  pending: number;
  reviewed: number;
  interview: number;
  accepted: number;
  rejected: number;
  cancelled: number;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: unknown;
}

export interface ApplicationListDto {
  items: ApplicationResponseDto[];
  total: number;
  page: number;
  pageSize: number;
}

export const STATUS_LABELS: Record<ApplicationStatus, string> = {
  PENDING: 'Pending',
  REVIEWED: 'Reviewed',
  INTERVIEW: 'Interview',
  ACCEPTED: 'Accepted',
  REJECTED: 'Rejected',
  CANCELLED: 'Cancelled',
};

export const STATUS_ORDER: ApplicationStatus[] = [
  'PENDING',
  'REVIEWED',
  'INTERVIEW',
  'ACCEPTED',
  'REJECTED',
  'CANCELLED',
];
