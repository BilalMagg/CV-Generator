import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ReminderService } from '../../../services/reminder.service';
import { AuthService } from '../../../services/auth.service';
import {
  CreateReminderDto,
  ReminderResultDto,
  ReminderOffsetType,
  REMINDER_OFFSET_OPTIONS,
} from '../../../models/reminder.model';

interface CalendarEvent {
  type: 'interview' | 'follow-up' | 'deadline';
  title: string;
}

interface CalendarDay {
  day: number;
  month: number;
  year: number;
  isCurrentMonth: boolean;
  isToday: boolean;
}

// We will use ReminderResultDto from the model instead

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.scss',
})
export class CalendarComponent implements OnInit {
  private reminderSvc = inject(ReminderService);
  private authSvc = inject(AuthService);

  currentMonth = signal(new Date());
  viewMode = signal<'month' | 'week'>('month');
  
  // Real reminders from service
  remindersList = signal<ReminderResultDto[]>([]);
  offsetOptions = REMINDER_OFFSET_OPTIONS;
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  successMsg = signal('');

  // Form state
  showForm = signal(false);
  form = {
    title: '',
    message: '',
    eventDate: '',
    reminderOffset: 'OneDay' as ReminderOffsetType,
  };

  async ngOnInit() {
    await this.loadReminders();
  }

  async loadReminders() {
    this.loading.set(true);
    this.error.set('');
    try {
      const user = this.authSvc.currentUser();
      if (user) {
        const data = await this.reminderSvc.getUserReminders(user.userId);
        this.remindersList.set(data);
      }
    } catch (e: any) {
      this.error.set('Failed to load reminders');
      console.error(e);
    } finally {
      this.loading.set(false);
    }
  }

  toggleForm() {
    this.showForm.update(v => !v);
    this.error.set('');
    this.successMsg.set('');
  }

  async submit() {
    if (!this.form.title || !this.form.eventDate) {
      this.error.set('Title and event date are required');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.successMsg.set('');

    try {
      const user = this.authSvc.currentUser();

      const dto: CreateReminderDto = {
        userId: user?.userId || '',
        userEmail: user?.email || '',
        userFirstName: user?.firstName || '',
        title: this.form.title,
        message: this.form.message || undefined,
        eventDate: new Date(this.form.eventDate).toISOString(),
        reminderOffset: this.form.reminderOffset,
      };

      await this.reminderSvc.create(dto);
      this.successMsg.set('Reminder created successfully!');
      this.resetForm();
      await this.loadReminders();
    } catch (e: any) {
      this.error.set('Failed to create reminder');
      console.error(e);
    } finally {
      this.saving.set(false);
    }
  }

  async cancelReminder(reminderId: string) {
    try {
      await this.reminderSvc.cancel(reminderId);
      await this.loadReminders();
    } catch (e: any) {
      this.error.set('Failed to cancel reminder');
      console.error(e);
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Pending': return 'badge-pending';
      case 'Sent': return 'badge-sent';
      case 'Cancelled': return 'badge-cancelled';
      case 'Failed': return 'badge-failed';
      default: return '';
    }
  }

  getOffsetLabel(offset: string): string {
    return this.offsetOptions.find(o => o.value === offset)?.label || offset;
  }

  private resetForm() {
    this.form = { title: '', message: '', eventDate: '', reminderOffset: 'OneDay' };
    this.showForm.set(false);
  }

  monthYear = computed(() => {
    const d = this.currentMonth();
    const months = ['January','February','March','April','May','June','July','August','September','October','November','December'];
    return `${months[d.getMonth()]} ${d.getFullYear()}`;
  });

  calendarDays = computed(() => {
    const d = this.currentMonth();
    const year = d.getFullYear();
    const month = d.getMonth();
    const firstDay = new Date(year, month, 1);
    const startOffset = (firstDay.getDay() + 6) % 7;
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const daysInPrev = new Date(year, month, 0).getDate();
    const today = new Date();

    const cells: CalendarDay[] = [];

    for (let i = startOffset - 1; i >= 0; i--) {
      cells.push({ day: daysInPrev - i, month: month - 1, year: month === 0 ? year - 1 : year, isCurrentMonth: false, isToday: false });
    }

    for (let day = 1; day <= daysInMonth; day++) {
      const isToday = year === today.getFullYear() && month === today.getMonth() && day === today.getDate();
      cells.push({ day, month, year, isCurrentMonth: true, isToday });
    }

    const remaining = 7 - (cells.length % 7 || 7);
    if (remaining < 7) {
      for (let day = 1; day <= remaining; day++) {
        cells.push({ day, month: month + 1, year: month === 11 ? year + 1 : year, isCurrentMonth: false, isToday: false });
      }
    }

    return cells;
  });

  eventsForDay(day: number): CalendarEvent[] {
    const d = this.currentMonth();
    const targetDate = new Date(d.getFullYear(), d.getMonth(), day);
    
    return this.remindersList()
      .filter(r => {
        const rDate = new Date(r.eventDate);
        return rDate.getDate() === targetDate.getDate() &&
               rDate.getMonth() === targetDate.getMonth() &&
               rDate.getFullYear() === targetDate.getFullYear();
      })
      .map(r => ({
        // Map reminder to a type for the legend colors
        // Since ReminderResultDto doesn't have an explicit 'type', 
        // we can guess from title or just use a default 'interview' color for now,
        // or if the model had a type I would use it.
        // Looking at previous code, it used Interview, Follow-up, Deadline.
        type: this.getEventTypeFromTitle(r.title),
        title: r.title
      }));
  }

  private getEventTypeFromTitle(title: string): 'interview' | 'follow-up' | 'deadline' {
    const t = title.toLowerCase();
    if (t.includes('interview')) return 'interview';
    if (t.includes('follow') || t.includes('follow-up')) return 'follow-up';
    if (t.includes('deadline') || t.includes('due')) return 'deadline';
    return 'interview'; // default
  }

  prevMonth() {
    const d = new Date(this.currentMonth());
    d.setMonth(d.getMonth() - 1);
    this.currentMonth.set(d);
  }

  nextMonth() {
    const d = new Date(this.currentMonth());
    d.setMonth(d.getMonth() + 1);
    this.currentMonth.set(d);
  }

  toggleView(view: 'month' | 'week') {
    this.viewMode.set(view);
  }

  toggleReminder(id: string) {
    // This was for mock data, maybe keep it or remove it?
    // The user wants real data, so I'll remove it.
  }
}
