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

  getPreferences(): Promise<ApiResponse<NotificationPreference>> {
    return this.http.get<ApiResponse<NotificationPreference>>(
      '/api/notifications/preferences',
    );
  }

  updatePreferences(
    dto: UpdateNotificationPreferenceDto,
  ): Promise<ApiResponse<NotificationPreference>> {
    return this.http.put<ApiResponse<NotificationPreference>>(
      '/api/notifications/preferences',
      dto,
    );
  }
}
