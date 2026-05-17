import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import {
  NotificationPreference,
  UpdateNotificationPreferenceDto,
} from '../models/notification-preference.model';

@Injectable({
  providedIn: 'root',
})
export class NotificationService {
  private readonly http = inject(HttpService);

  getPreferences(userId: string): Promise<NotificationPreference> {
    return this.http.get<NotificationPreference>(
      `/api/notifications/${userId}/preferences`,
    );
  }

  updatePreferences(
    userId: string,
    dto: UpdateNotificationPreferenceDto,
  ): Promise<NotificationPreference> {
    return this.http.put<NotificationPreference>(
      `/api/notifications/${userId}/preferences`,
      dto,
    );
  }
}
