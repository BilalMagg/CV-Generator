import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApplicationService } from '../../../services/application.service';
import { ApplicationResponseDto, ApplicationStatus } from '../../../models/application.model';

interface Column {
  status: ApplicationStatus;
  label: string;
  colorVar: string;
  items: ApplicationResponseDto[];
}

const COLUMNS: { status: ApplicationStatus; label: string; colorVar: string }[] = [
  { status: 'PENDING',   label: 'Applied',   colorVar: 'applied' },
  { status: 'REVIEWED',  label: 'Screening', colorVar: 'screening' },
  { status: 'INTERVIEW', label: 'Interview', colorVar: 'interview' },
  { status: 'ACCEPTED',  label: 'Offer',     colorVar: 'offer' },
  { status: 'REJECTED',  label: 'Rejected',  colorVar: 'rejected' },
  { status: 'CANCELLED', label: 'Cancelled', colorVar: 'cancelled' },
];

@Component({
  selector: 'app-kanban',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './kanban.component.html',
  styleUrl: './kanban.component.scss',
})
export class KanbanComponent implements OnInit {
  private appService = inject(ApplicationService);

  columns = signal<Column[]>(COLUMNS.map(c => ({ ...c, items: [] })));
  loading = signal(true);
  view = signal<'board' | 'list'>('board');

  totalApps = computed(() => this.columns().reduce((s, c) => s + c.items.length, 0));

  ngOnInit() { this.loadApps(); }

  async loadApps() {
    this.loading.set(true);
    try {
      const res = await this.appService.getAll({ pageSize: 200 });
      if (res.success && res.data) {
        const all = res.data.items;
        this.columns.set(COLUMNS.map(col => ({
          ...col,
          items: all.filter(a => a.status === col.status),
        })));
      }
    } catch { } finally { this.loading.set(false); }
  }

  onDragStart(event: DragEvent, app: ApplicationResponseDto) {
    event.dataTransfer?.setData('text/plain', JSON.stringify({ id: app.id, status: app.status }));
    (event.target as HTMLElement).classList.add('dragging');
  }

  onDragEnd(event: DragEvent) {
    (event.target as HTMLElement).classList.remove('dragging');
  }

  onDragOver(event: DragEvent) { event.preventDefault(); }

  async onDrop(event: DragEvent, targetStatus: ApplicationStatus) {
    event.preventDefault();
    const data = event.dataTransfer?.getData('text/plain');
    if (!data) return;
    const { id, status: fromStatus } = JSON.parse(data);
    if (fromStatus === targetStatus) return;

    const col = this.columns().find(c => c.status === fromStatus);
    const app = col?.items.find(a => a.id === id);
    if (!app) return;

    this.columns.update(cols => cols.map(c => {
      if (c.status === fromStatus) return { ...c, items: c.items.filter(a => a.id !== id) };
      if (c.status === targetStatus) return { ...c, items: [{ ...app, status: targetStatus }, ...c.items] };
      return c;
    }));

    try {
      await this.appService.updateStatus(id, { status: targetStatus });
    } catch {
      this.loadApps();
    }
  }

  logoBg(name: string): string {
    const palette = [
      'oklch(0.93 0.04 250)', 'oklch(0.93 0.04 160)', 'oklch(0.93 0.04 65)',
      'oklch(0.93 0.04 300)', 'oklch(0.93 0.04 25)',  'oklch(0.94 0.02 80)',
    ];
    let h = 0;
    for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
    return palette[Math.abs(h) % palette.length];
  }

  logoText(name: string): string {
    return name.slice(0, 2).toUpperCase();
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }

  allItems = computed(() => this.columns().flatMap(c => c.items));
}
