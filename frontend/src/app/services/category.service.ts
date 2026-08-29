import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { CategorySearchRequest, CategorySearchResult, CategoryTagRequest, TaxonomyNode } from '@app/models/category.model';
import { ApiResponse } from '@app/models/application.model';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private http = inject(HttpService);

  async getTree(scope: string): Promise<TaxonomyNode[]> {
    const res = await this.http.get<ApiResponse<TaxonomyNode[]>>(`/api/categories/tree?scope=${encodeURIComponent(scope)}`);
    return res.data ?? [];
  }

  async search(req: CategorySearchRequest): Promise<CategorySearchResult[]> {
    const res = await this.http.post<ApiResponse<CategorySearchResult[]>>('/api/categories/search', req);
    return res.data ?? [];
  }

  async getTags(sourceType: string, sourceId: string): Promise<string[]> {
    const res = await this.http.get<ApiResponse<string[]>>(
      `/api/categories/tags?sourceType=${encodeURIComponent(sourceType)}&sourceId=${encodeURIComponent(sourceId)}`,
    );
    return (res.data ?? []).map(String);
  }

  async setTags(req: CategoryTagRequest): Promise<unknown> {
    return this.http.put<unknown>('/api/categories/tags', req);
  }
}
