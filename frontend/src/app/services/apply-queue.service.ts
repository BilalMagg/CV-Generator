import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '../models/application.model';
import {
  ApplyQueueItemDto,
  ExecuteApplyQueueResult,
  SaveApplyQueueItemDto,
} from '../models/apply-queue.model';

@Injectable({ providedIn: 'root' })
export class ApplyQueueService {
  private readonly http = inject(HttpService);

  list(status: string = 'pending'): Promise<ApiResponse<ApplyQueueItemDto[]>> {
    const q = status && status !== 'all' ? `?status=${encodeURIComponent(status)}` : '?status=all';
    return this.http.get<ApiResponse<ApplyQueueItemDto[]>>(`/api/applications/queue${q}`);
  }

  save(dto: SaveApplyQueueItemDto): Promise<ApiResponse<ApplyQueueItemDto>> {
    return this.http.post<ApiResponse<ApplyQueueItemDto>>('/api/applications/queue', dto);
  }

  /**
   * Stages several applies at once (the bulk-apply wizard saves its whole row set).
   * Resolves to the number of items actually created.
   */
  async saveBatch(dtos: SaveApplyQueueItemDto[]): Promise<number> {
    if (dtos.length === 1) {
      await this.save(dtos[0]);
      return 1;
    }
    const res = await this.http.post<ApiResponse<ApplyQueueItemDto[]>>(
      '/api/applications/queue/batch',
      dtos as unknown as object,
    );
    return res.data?.length ?? 0;
  }

  remove(id: string): Promise<ApiResponse<unknown>> {
    return this.http.delete<ApiResponse<unknown>>(`/api/applications/queue/${id}`);
  }

  /** Executes a selection as a bulk-apply run. Returns the run id for the redirect. */
  execute(itemIds: string[], cvVersionId?: string, delaySeconds = 5): Promise<ApiResponse<ExecuteApplyQueueResult>> {
    return this.http.post<ApiResponse<ExecuteApplyQueueResult>>('/api/applications/queue/execute', {
      itemIds,
      cvVersionId: cvVersionId || null,
      delaySeconds,
    });
  }
}
