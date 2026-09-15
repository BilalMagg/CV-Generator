export type LinkedInToolType = 'post' | 'comment' | 'message';
export type ToolTab = LinkedInToolType | 'emojify';
export type LinkedInTone = 'professional' | 'enthusiastic' | 'storytelling' | 'honest';
export type LinkedInLength = 'short' | 'medium' | 'long';
export type EmojifyDensity = 'low' | 'medium' | 'high';

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
  baseText?: string;
  adjustment?: string;
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

export interface EmojifyRequest {
  text: string;
  hint?: string;
  density: EmojifyDensity;
  language?: string;
  variants?: number;
}

export interface EmojifyVariant {
  text: string;
}

export interface EmojifyResult {
  variants: EmojifyVariant[];
}