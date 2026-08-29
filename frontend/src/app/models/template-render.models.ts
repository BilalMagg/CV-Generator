import { StepStatus } from './cv-generation.models';

export type TemplateRenderRunStatus = 'pending' | 'running' | 'completed' | 'failed' | 'cancelled';

export interface TemplateRenderStatus {
  run_id: string;
  status: TemplateRenderRunStatus;
  current_step: number;
  steps: StepStatus[];
  error_message: string | null;
  created_at: string;
  completed_at: string | null;
  cancelled_at: string | null;
}

export interface TemplateRenderResult {
  run_id: string;
  extraction: any;
  search: any;
  render: any;
  tex: string;
  pdf_url: string | null;
  cv_id: string | null;
  cv_version_id: string | null;
  saved: boolean;
  match_score: number | null;
  gap_skills: string[];
}

export interface TemplateRenderSubmitResponse {
  run_id: string;
}

export interface TemplateRenderSseSnapshot {
  type: 'snapshot';
  status: TemplateRenderRunStatus;
  current_step: number;
  steps: StepStatus[];
  error_message?: string | null;
}

export interface TemplateRenderSseDone {
  type: 'done';
  status: TemplateRenderRunStatus;
  steps: StepStatus[];
  error_message?: string | null;
  result?: TemplateRenderResult;
}

export interface TemplateRenderSseError {
  type: 'error';
  error_message?: string;
}

export type TemplateRenderSseEvent = TemplateRenderSseSnapshot | TemplateRenderSseDone | TemplateRenderSseError;

// The 4 rows shown in the page's step list. Step 0 (Job Extraction) is
// satisfied before the run starts (fresh extraction or a picked history run);
// steps 1-3 map 1:1 to the backend run steps.
export const TEMPLATE_RENDER_RUN_STEPS: ReadonlyArray<{ name: string; runStep?: number }> = [
  { name: 'Job Extraction' },
  { name: 'Profile Matching', runStep: 0 },
  { name: 'Template Rendering', runStep: 1 },
  { name: 'PDF & Save', runStep: 2 },
];