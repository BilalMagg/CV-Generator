export interface CreateReminderDto {
  userId: string;
  userEmail: string;
  userFirstName: string;
  title: string;
  message?: string;
  eventDate: string;       // ISO date string
  reminderOffset: ReminderOffsetType;
}

export interface ReminderResultDto {
  id: string;
  title: string;
  message?: string;
  eventDate: string;
  reminderOffset: string;
  reminderAt: string;
  status: ReminderStatusType;
  createdAt: string;
  sentAt?: string;
}

export type ReminderOffsetType = 'None' | 'OneDay' | 'TwoDays' | 'ThreeDays' | 'OneWeek';
export type ReminderStatusType = 'Pending' | 'Sent' | 'Cancelled' | 'Failed';

export const REMINDER_OFFSET_OPTIONS: { value: ReminderOffsetType; label: string }[] = [
  { value: 'None', label: 'On the event day' },
  { value: 'OneDay', label: '1 day before' },
  { value: 'TwoDays', label: '2 days before' },
  { value: 'ThreeDays', label: '3 days before' },
  { value: 'OneWeek', label: '1 week before' },
];
