export interface CalendarConfigurationDto {
  id: string;
  userId: string;
  showReminders: boolean;
  selectedStatuses: string[];
}

export interface UpdateCalendarConfigurationDto {
  showReminders: boolean;
  selectedStatuses: string[];
}
