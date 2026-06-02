import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { CreateReminderDto, ReminderResultDto } from '../models/reminder.model';

@Injectable({
  providedIn: 'root',
})
export class ReminderService {
  private http = inject(HttpService);

  async create(dto: CreateReminderDto): Promise<{ id: string; message: string }> {
    return this.http.post<{ id: string; message: string }>('/api/reminders', dto);
  }

  async getUserReminders(userId: string): Promise<ReminderResultDto[]> {
    return this.http.get<ReminderResultDto[]>(`/api/reminders/${userId}`);
  }

  async cancel(reminderId: string): Promise<void> {
    return this.http.delete<void>(`/api/reminders/${reminderId}`);
  }
}
