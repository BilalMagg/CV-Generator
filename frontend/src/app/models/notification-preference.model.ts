export interface NotificationPreference {
  id: string;
  userId: string;
  enableEmail: boolean;
  enableInApp: boolean;
  reminders: boolean;
  applicationUpdates: boolean;
  cvUpdates: boolean;
  weeklyDigest: boolean;
  defaultReminderDaysBefore: number;
  updatedAt: string;
}

export interface UpdateNotificationPreferenceDto {
  enableEmail?: boolean | null;
  enableInApp?: boolean | null;
  reminders?: boolean | null;
  applicationUpdates?: boolean | null;
  cvUpdates?: boolean | null;
  weeklyDigest?: boolean | null;
  defaultReminderDaysBefore?: number | null;
}
