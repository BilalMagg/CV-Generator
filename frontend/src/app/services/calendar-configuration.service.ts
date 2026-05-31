import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { ApiResponse } from '@app/models/application.model';
import { CalendarConfigurationDto, UpdateCalendarConfigurationDto } from '@app/models/calendar-configuration.model';

@Injectable({
  providedIn: 'root',
})
export class CalendarConfigurationService {
  private http = inject(HttpService);

  async getConfiguration(): Promise<ApiResponse<CalendarConfigurationDto>> {
    return this.http.get<ApiResponse<CalendarConfigurationDto>>('/api/applications/calendar-configuration');
  }

  async updateConfiguration(dto: UpdateCalendarConfigurationDto): Promise<ApiResponse<CalendarConfigurationDto>> {
    return this.http.put<ApiResponse<CalendarConfigurationDto>>('/api/applications/calendar-configuration', dto);
  }
}
