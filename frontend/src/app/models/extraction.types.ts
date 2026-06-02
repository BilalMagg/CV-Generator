export interface ExtractorOutput {
  enterpriseName?: string;
  enterpriseDescription?: string;
  enterpriseLogoUrl?: string;
  jobRole?: string;
  rawDescription?: string;
  responsibilities: string[];
  requiredSkills: string[];
  softSkills: string[];
  requiredExperienceYears?: number;
  seniorityLevel?: string;
  employmentType?: string;
  location?: string;
  locationType?: string;
  salaryRange?: string;
  currency?: string;
  certifications: string[];
  languages: string[];
  educationRequirements?: string;
  benefits: string[];
  applicationDeadline?: string;
  contactEmail?: string;
  sourceUrl?: string;
  fieldConfidences: Record<string, number>;
  overallConfidence: number;
}

export interface ExtractionHistoryItem {
  id: string;
  jobRole: string;
  companyName: string;
  createdAt: string;
  overallConfidence: number;
  sourceType: string;
}

export interface AgentConfig {
  model: string;
  temperature: number;
  apiKey: string;
  defaultLanguage: string;
  confidenceThreshold: number;
  defaultExportFormat: 'json' | 'csv';
  autoSave: boolean;
}

export const MOCK_EXTRACTION: ExtractorOutput = {
  enterpriseName: 'TechCorp',
  enterpriseDescription: 'TechCorp is a leading provider of cloud-based enterprise solutions, serving over 10,000 customers worldwide.',
  enterpriseLogoUrl: '',
  jobRole: 'Senior Frontend Engineer',
  rawDescription: 'We are looking for a Senior Frontend Engineer to join our team...',
  responsibilities: [
    'Build and maintain responsive web applications using Angular',
    'Collaborate with UX designers to implement pixel-perfect interfaces',
    'Optimize application performance and accessibility',
    'Mentor junior developers through code reviews',
    'Contribute to our component library and design system',
  ],
  requiredSkills: ['Angular', 'TypeScript', 'RxJS', 'SCSS', 'NgRx', 'Jest', 'Git'],
  softSkills: ['Communication', 'Leadership', 'Problem-solving', 'Team collaboration'],
  requiredExperienceYears: 5,
  seniorityLevel: 'Senior',
  employmentType: 'Full-time',
  location: 'San Francisco, CA',
  locationType: 'Hybrid',
  salaryRange: '150,000 - 180,000',
  currency: 'USD',
  certifications: ['AWS Certified Developer'],
  languages: ['English (Native)', 'Spanish (Professional)'],
  educationRequirements: "Bachelor's degree in Computer Science or related field",
  benefits: [
    'Health, dental, and vision insurance',
    'Unlimited PTO',
    'Stock options',
    '401(k) matching',
    'Remote work flexibility',
  ],
  applicationDeadline: '2026-07-15',
  contactEmail: 'careers@techcorp.com',
  sourceUrl: 'https://techcorp.com/careers/senior-frontend-engineer',
  fieldConfidences: {
    enterpriseName: 0.95,
    jobRole: 0.98,
    responsibilities: 0.88,
    requiredSkills: 0.92,
    requiredExperienceYears: 0.85,
    seniorityLevel: 0.97,
    employmentType: 0.99,
    salaryRange: 0.75,
  },
  overallConfidence: 0.86,
};

export const MOCK_HISTORY: ExtractionHistoryItem[] = [
  { id: '1', jobRole: 'Senior Frontend Engineer', companyName: 'TechCorp', createdAt: '2026-05-25T14:30:00Z', overallConfidence: 0.86, sourceType: 'text' },
  { id: '2', jobRole: 'Backend Developer', companyName: 'DataFlow Inc', createdAt: '2026-05-24T10:15:00Z', overallConfidence: 0.92, sourceType: 'url' },
  { id: '3', jobRole: 'Product Designer', companyName: 'DesignLab', createdAt: '2026-05-23T16:45:00Z', overallConfidence: 0.78, sourceType: 'file' },
  { id: '4', jobRole: 'DevOps Engineer', companyName: 'CloudBase', createdAt: '2026-05-22T09:00:00Z', overallConfidence: 0.94, sourceType: 'offer' },
  { id: '5', jobRole: 'Data Scientist', companyName: 'AI Solutions', createdAt: '2026-05-21T11:30:00Z', overallConfidence: 0.71, sourceType: 'text' },
];

export const DEFAULT_CONFIG: AgentConfig = {
  model: 'llama-3.1-8b-instant',
  temperature: 0,
  apiKey: '',
  defaultLanguage: 'en',
  confidenceThreshold: 0,
  defaultExportFormat: 'json',
  autoSave: true,
};
