export interface CVProfile {
  id?: string;
  fullName: string;
  title?: string;
  summary?: string;
  location?: string;
  phoneNumber?: string;
  email?: string;
  website?: string;
  userId?: string;
}

export interface Project {
  id?: string;
  title: string;
  description?: string;
  role?: string;
  achievements?: string;
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
  position: string;
  location?: string;
  description?: string;
  startDate: string;
  endDate?: string;
  isCurrent: boolean;
  userId?: string;
}

export interface Education {
  id?: string;
  institution: string;
  degree: string;
  fieldOfStudy?: string;
  startDate: string;
  endDate?: string;
  grade?: string;
  description?: string;
  userId?: string;
}

export interface Certification {
  id?: string;
  name: string;
  issuer: string;
  issueDate: string;
  expirationDate?: string;
  credentialId?: string;
  credentialUrl?: string;
  userId?: string;
}

export interface Language {
  id?: string;
  name: string;
  proficiency: string;
  userId?: string;
}

export interface Interest {
  id?: string;
  name: string;
  category?: string;
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
  role?: string;
  description?: string;
  date: string;
  location?: string;
  achievement?: string;
  userId?: string;
}

export type EntityType = 
  | 'cvprofile' 
  | 'projects' 
  | 'skills' 
  | 'experiences' 
  | 'education' 
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
  cvprofile: [
    { name: 'fullName', label: 'Full Name', type: 'text', required: true },
    { name: 'title', label: 'Job Title', type: 'text' },
    { name: 'summary', label: 'Summary', type: 'textarea' },
    { name: 'location', label: 'Location', type: 'text' },
    { name: 'email', label: 'Email', type: 'text' },
    { name: 'phoneNumber', label: 'Phone Number', type: 'text' },
    { name: 'website', label: 'Website', type: 'text' },
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
  ],
  skills: [
    { name: 'name', label: 'Skill Name', type: 'text', required: true },
    { name: 'category', label: 'Category', type: 'text', placeholder: 'e.g. Frontend, Soft Skill' },
    { name: 'level', label: 'Level', type: 'select', options: ['Beginner', 'Intermediate', 'Advanced', 'Expert'] },
    { name: 'yearsOfExperience', label: 'Years of Experience', type: 'number' },
  ],
  experiences: [
    { name: 'company', label: 'Company', type: 'text', required: true },
    { name: 'position', label: 'Position', type: 'text', required: true },
    { name: 'location', label: 'Location', type: 'text' },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'isCurrent', label: 'Currently working here', type: 'checkbox' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ],
  education: [
    { name: 'institution', label: 'Institution', type: 'text', required: true },
    { name: 'degree', label: 'Degree', type: 'text', required: true },
    { name: 'fieldOfStudy', label: 'Field of Study', type: 'text' },
    { name: 'startDate', label: 'Start Date', type: 'date', required: true },
    { name: 'endDate', label: 'End Date', type: 'date' },
    { name: 'grade', label: 'Grade / GPA', type: 'text' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ],
  certifications: [
    { name: 'name', label: 'Certification Name', type: 'text', required: true },
    { name: 'issuer', label: 'Issuer', type: 'text', required: true },
    { name: 'issueDate', label: 'Issue Date', type: 'date', required: true },
    { name: 'expirationDate', label: 'Expiration Date', type: 'date' },
    { name: 'credentialId', label: 'Credential ID', type: 'text' },
    { name: 'credentialUrl', label: 'Credential URL', type: 'text' },
  ],
  languages: [
    { name: 'name', label: 'Language', type: 'text', required: true },
    { name: 'proficiency', label: 'Proficiency', type: 'select', options: ['Native', 'Fluent', 'Professional', 'Intermediate', 'Elementary'] },
  ],
  interests: [
    { name: 'name', label: 'Interest', type: 'text', required: true },
    { name: 'category', label: 'Category', type: 'text' },
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
    { name: 'role', label: 'Your Role', type: 'text' },
    { name: 'date', label: 'Date', type: 'date', required: true },
    { name: 'location', label: 'Location', type: 'text' },
    { name: 'achievement', label: 'Achievement', type: 'text' },
    { name: 'description', label: 'Description', type: 'textarea' },
  ],
};
