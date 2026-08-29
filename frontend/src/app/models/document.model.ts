export interface CvVersionDto {
  id: string;
  cvId: string;
  versionNumber: number;
  label: string;
  fileUrl?: string | null;
  pdfUrl?: string | null;
  thumbnailUrl?: string | null;
  contentJson: string;
  createdAt: string;
}

export interface CvDocumentDto {
  id: string;
  userId: string;
  title: string;
  templateId: string;
  createdAt: string;
  updatedAt: string;
  isActive: boolean;
  versions: CvVersionDto[];
}

export interface CvUpdateInput {
  title: string;
  templateId?: string | null;
  isActive: boolean;
}

export interface CvTemplateDto {
  id: string;
  name: string;
  description: string;
  templateType: 'latex' | 'html' | 'pdf';
  content: string;
  isSystem: boolean;
  userId?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CvTemplateInput {
  name: string;
  description?: string;
  templateType: 'latex' | 'html' | 'pdf';
  content: string;
}