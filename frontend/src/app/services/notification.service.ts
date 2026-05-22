import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import {
  NotificationPreference,
  UpdateNotificationPreferenceDto,
} from '../models/notification-preference.model';
import { ApiResponse } from '../models/application.model';

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private readonly http = inject(HttpService);

  getPreferences(userId: string): Promise<ApiResponse<NotificationPreference>> {
    return this.http.get<ApiResponse<NotificationPreference>>(
      `/api/notifications/${userId}/preferences`,
    );
  }

  updatePreferences(
    userId: string,
    dto: UpdateNotificationPreferenceDto,
  ): Promise<ApiResponse<NotificationPreference>> {
    return this.http.put<ApiResponse<NotificationPreference>>(
      `/api/notifications/${userId}/preferences`,
      dto,
    );
  }
}
