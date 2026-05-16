export interface CVProfile {
  id?: string;
  title: string; // Job title (ex: Senior Web Developer)
  summary: string; // Professional Bio
  userId?: string;
}

export interface Project {
  id?: string;
  title: string;
  description?: string;
  role?: string;
  startDate: string;
  endDate?: string;
  repositoryUrl?: string;
  demoUrl?: string;
  status: string;
  skillsJson?: string;
  userId?: string;
}

export interface Skill {
  id?: string;
  name: string;
  level?: string;
  yearsOfExperience?: number;
  category?: string;
  userId?: string;
}

export interface Experience {
  id?: string;
  company: string;
  title: string;
  description?: string;
  startDate: string;
  endDate?: string;
  status: string;
  userId?: string;
}

export interface Education {
  id?: string;
  institution?: string;
  institutionName?: string;
  degree?: string;
  degreeType?: string;
  fieldOfStudy?: string;
  specialization?: string;
  startDate: string;
  endDate?: string;
  grade?: string;
  description?: string;
  status?: string;
  city?: string;
  userId?: string;
  DiplomaFileUrl?: string;
}

export interface Certification {
  id?: string;
  name: string;
  issuingOrganization?: string; // Backend field name
  issueDate?: string;
  credentialUrl?: string;
  userId?: string;
}

export interface Language {
  id?: string;
  name: string;
  level: string;
  userId?: string;
}

export interface Interest {
  id?: string;
  name: string;
  userId?: string;
}

export interface SocialLink {
  id?: string;
  platform: string;
  url: string;
  userId?: string;
}

export interface AcademicActivity {
  id?: string;
  title: string;
  organization?: string;
  description?: string;
  startDate: string;
  endDate?: string;
  userId?: string;
}

export interface Hackathon {
  id?: string;
  name: string;
  organization?: string; // Backend field name
  date?: string;
  description?: string;
  role?: string;
  result?: string; // Backend field name (e.g. "Winner", "Finalist")
  userId?: string;
}

export type EntityType = 
  | 'cvprofiles' 
  | 'projects' 
  | 'skills' 
  | 'experiences' 
  | 'educations' 
  | 'certifications' 
  | 'languages' 
  | 'interests' 
  | 'sociallinks' 
  | 'academicactivities' 
  | 'hackathons';

export interface FieldConfig {
  name: string;
  label: string;
  type: 'text' | 'textarea' | 'date' | 'number' | 'select' | 'checkbox';
  placeholder?: string;
  options?: string[];
  required?: boolean;
}

export const ENTITY_FIELDS: Record<EntityType, FieldConfig[]> = {
  cvprofiles: [
    { name: 'title', label: 'Job Title', type: 'text', required: true, placeholder: 'e.g. Senior Web Developer' },
    { name: 'summary', label: 'Professional Summary', type: 'textarea', required: true },
  ],
  projects: [
    { name: 'title', label: 'Project Title', type: 'text', required: true },
    { name: 'description', label: 'Description', type: 'textarea' },
    { name: 'role', label: 'Your Role', type: 'text' },
    { name: 'status', label: 'Status', type: 'select', options: ['Ongoing', 'Completed', 'On Hold'] },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'repositoryUrl', label: 'Repository URL', type: 'text' },
    { name: 'demoUrl', label: 'Demo URL', type: 'text' },
    { name: 'skillsJson', label: 'Skills ', type: 'textarea' },
  ],
  skills: [
    { name: 'name', label: 'Skill Name', type: 'text', required: true },
    { name: 'category', label: 'Category', type: 'text', placeholder: 'e.g. Frontend, Soft Skill' },
    { name: 'level', label: 'Level', type: 'select', options: ['Beginner', 'Intermediate', 'Advanced', 'Expert'] },
    { name: 'yearsOfExperience', label: 'Years of Experience', type: 'number' },
  ],
  experiences: [
    { name: 'company', label: 'Company', type: 'text', required: true },
    { name: 'title', label: 'title', type: 'text', required: true },
    { name: 'status', label: 'Status', type: 'select', options: ['Ongoing', 'Completed'] },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ],
  educations: [
    { name: 'institutionName', label: 'Institution', type: 'text', required: true },
    { name: 'degreeType', label: 'Degree Type', type: 'text', required: true },
    { name: 'fieldOfStudy', label: 'Field of Study', type: 'text', required: true },
    { name: 'specialization', label: 'Specialization', type: 'text' },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'status', label: 'Status', type: 'select', options: ['Ongoing', 'Completed'] },
    { name: 'city', label: 'City', type: 'text' },
    { name: 'DiplomaFileUrl', label: 'Diploma File URL', type: 'text' },
  ],
  certifications: [
    { name: 'name', label: 'Certification Name', type: 'text', required: true },
    { name: 'issuingOrganization', label: 'Issuing Organization', type: 'text' },
    { name: 'issueDate', label: 'Issue Date', type: 'date' },
    { name: 'credentialUrl', label: 'Credential URL', type: 'text' },
  ],
  languages: [
    { name: 'name', label: 'Language', type: 'text', required: true },
    { name: 'level', label: 'Level', type: 'select', options: ['Native', 'Fluent', 'Professional', 'Intermediate', 'Elementary'] },
  ],
  interests: [
    { name: 'name', label: 'Interest', type: 'text', required: true },
  ],
  sociallinks: [
    { name: 'platform', label: 'Platform', type: 'text', required: true, placeholder: 'e.g. LinkedIn, GitHub' },
    { name: 'url', label: 'URL', type: 'text', required: true },
  ],
  academicactivities: [
    { name: 'title', label: 'Title', type: 'text', required: true },
    { name: 'organization', label: 'Organization', type: 'text' },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ],
  hackathons: [
    { name: 'name', label: 'Hackathon Name', type: 'text', required: true },
    { name: 'organization', label: 'Organization', type: 'text' },
    { name: 'date', label: 'Date', type: 'date' },
    { name: 'description', label: 'Description', type: 'textarea' },
    { name: 'role', label: 'Your Role', type: 'text' },
    { name: 'result', label: 'Result', type: 'text', placeholder: 'e.g. Winner, Finalist' },
  ],
};
