export interface EmployeeDto {
  id: string;
  userId: string;
  name: string;
  position?: string;
  company?: string;
  email?: string;
  phone?: string;
  linkedInUrl?: string;
  notes?: string;
  source: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateEmployeeDto {
  name: string;
  position?: string;
  company?: string;
  email?: string;
  phone?: string;
  linkedInUrl?: string;
  notes?: string;
}

export interface UpdateEmployeeDto {
  name?: string;
  position?: string;
  company?: string;
  email?: string;
  phone?: string;
  linkedInUrl?: string;
  notes?: string;
}

export interface EmployeeExtractResult {
  imported: number;
  skipped: number;
  errors: string[];
}