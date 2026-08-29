export interface TaxonomyNode {
  id: string;
  scope: string;
  parentId: string | null;
  name: string;
  domain: string | null;
  level: number;
  path: string;
  keywords: string[];
  isSystem: boolean;
  count?: number;
  children?: TaxonomyNode[];
}

export interface CategorySearchRequest {
  nodeIds: string[];
  sourceTypes?: string[];
}

export interface CategorySearchResult {
  sourceId: string;
  sourceType: string;
  score: number;
}

export interface CategoryTagRequest {
  sourceType: string;
  sourceId: string;
  nodeIds: string[];
}
