import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';

export interface SearchResult {
  sourceId: string;
  sourceType: string;
  content: string;
  score: number;
}

export interface SearchStatus {
  synced: boolean;
  chunkCount: number;
  sourceTypeCounts: Record<string, number>;
}

export interface SyncResult {
  chunksSynced: number;
}

@Injectable({ providedIn: 'root' })
export class SearchService {
  private readonly http = inject(HttpService);

  async search(query: string, sourceTypes: string[], limit = 15): Promise<SearchResult[]> {
    const resp = await this.http.post<{ success: boolean; data: SearchResult[] }>('/api/search', {
      query,
      sourceTypes,
      limit,
    });
    return resp.data ?? [];
  }

  async getStatus(): Promise<SearchStatus> {
    const resp = await this.http.get<{ success: boolean; data: SearchStatus }>('/api/search/status');
    return resp.data ?? { synced: false, chunkCount: 0, sourceTypeCounts: {} };
  }

  async sync(sourceTypes?: string[]): Promise<SyncResult> {
    const resp = await this.http.post<{ success: boolean; data: SyncResult }>('/api/search/sync', {
      sourceTypes: sourceTypes ?? [],
    });
    return resp.data ?? { chunksSynced: 0 };
  }
}
