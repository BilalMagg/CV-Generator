export interface StepStatus {
  step: number;
  name: string;
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';
  started_at: string | null;
  completed_at: string | null;
  duration_ms: number | null;
  error: string | null;
}

export interface CvGenerationStatus {
  run_id: string;
  status: 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';
  current_step: number;
  steps: StepStatus[];
  error_message: string | null;
  created_at: string;
  completed_at: string | null;
  cancelled_at: string | null;
}

export interface CvGenerationResult {
  run_id: string;
  extraction: any;
  search: any;
  optimization: any;
  render: any;
  delivery: any;
}

export interface CvGenerationSubmitResponse {
  run_id: string;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
}

export const STEP_ICONS: Record<string, string> = {
  'Job Extraction': 'ti ti-file-search',
  'Profile Matching': 'ti ti-users',
  'CV Optimization': 'ti ti-sparkles',
  'Template Rendering': 'ti ti-template',
  'Email Delivery': 'ti ti-mail',
};
