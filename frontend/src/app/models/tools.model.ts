export type LinkedInToolType = 'post' | 'comment' | 'message';
export type LinkedInTone = 'professional' | 'enthusiastic' | 'storytelling' | 'honest';
export type LinkedInLength = 'short' | 'medium' | 'long';

export interface LinkedInRequest {
  tool: LinkedInToolType;
  language: string;
  tone: LinkedInTone;
  length: LinkedInLength;
  variants: number;
  contextType?: string;
  context?: string;
  mentions?: string[];
  includeHashtags?: boolean;
  hashtagCount?: number;
  targetText?: string;
  points?: string[];
  recipientName?: string;
  relationship?: string;
  purpose?: string;
  recipientContext?: string;
  senderContext?: string;
}

export interface LinkedInVariant {
  title: string;
  text: string;
  hashtags: string;
}

export interface LinkedInResult {
  tool: string;
  variants: LinkedInVariant[];
}