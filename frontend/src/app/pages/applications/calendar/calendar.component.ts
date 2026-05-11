import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

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

interface Reminder {
  id: string;
  title: string;
  company: string;
  position: string;
  dateStr: string;
  timeStr: string;
  type: 'Interview' | 'Follow-up' | 'Deadline';
  done: boolean;
}

@Component({
  selector: 'app-calendar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './calendar.component.html',
  styleUrl: './calendar.component.scss',
})
export class CalendarComponent {
  currentMonth = signal(new Date());
  viewMode = signal<'month' | 'week'>('month');
  reminders = signal<Reminder[]>([
    { id: '1', title: 'Technical interview', company: 'Google', position: 'SWE II', dateStr: '15', timeStr: '10:00am', type: 'Interview', done: false },
    { id: '2', title: 'Send follow-up', company: 'Linear', position: 'Frontend Eng', dateStr: '18', timeStr: 'End of day', type: 'Follow-up', done: false },
    { id: '3', title: 'Final round interview', company: 'Meta', position: 'Product Designer', dateStr: '21', timeStr: '2:00pm', type: 'Interview', done: false },
    { id: '4', title: 'Follow-up sent', company: 'Google', position: 'SWE II', dateStr: '10', timeStr: 'Done', type: 'Follow-up', done: true },
  ]);

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
    const dayMap: Record<number, CalendarEvent[]> = {
      10: [{ type: 'follow-up', title: 'Follow-up' }],
      15: [{ type: 'interview', title: 'Interview' }],
      18: [{ type: 'deadline', title: 'Deadline' }],
      21: [{ type: 'interview', title: 'Interview' }, { type: 'follow-up', title: 'Follow-up' }],
      25: [{ type: 'follow-up', title: 'Follow-up' }],
    };
    if (d.getDate() === day) {
      const existing = dayMap[day] || [];
      if (!existing.some(e => e.type === 'interview' && e.title === 'Interview')) {
        existing.push({ type: 'interview', title: 'Interview' });
      }
    }
    return dayMap[day] || [];
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
    this.reminders.update(list => list.map(r => r.id === id ? { ...r, done: !r.done } : r));
  }
}
