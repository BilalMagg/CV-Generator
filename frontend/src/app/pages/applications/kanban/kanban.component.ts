import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApplicationService } from '../../../services/application.service';
import { ApplicationResponseDto, ApplicationStatus, STATUS_LABELS } from '../../../models/application.model';

interface Column {
  status: ApplicationStatus;
  label: string;
  items: ApplicationResponseDto[];
}

const COLUMNS: { status: ApplicationStatus; label: string }[] = [
  { status: 'PENDING', label: 'Pending' },
  { status: 'REVIEWED', label: 'Reviewed' },
  { status: 'INTERVIEW', label: 'Interview' },
  { status: 'ACCEPTED', label: 'Accepted' },
  { status: 'REJECTED', label: 'Rejected' },
  { status: 'CANCELLED', label: 'Cancelled' },
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

  ngOnInit() { this.loadApps(); }

  async loadApps() {
    this.loading.set(true);
    try {
      const res = await this.appService.getAll({ pageSize: 100 });
      if (res.success && res.data) {
        const allApps = res.data.items;
        this.columns.set(COLUMNS.map(col => ({
          ...col,
          items: allApps.filter(a => a.status === col.status),
        })));
      }
    } catch { } finally { this.loading.set(false); }
  }

  onDragStart(event: DragEvent, app: ApplicationResponseDto) {
    event.dataTransfer?.setData('text/plain', JSON.stringify({ id: app.id, status: app.status }));
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
  }

  async onDrop(event: DragEvent, targetColumn: ApplicationStatus) {
    event.preventDefault();
    const data = event.dataTransfer?.getData('text/plain');
    if (!data) return;
    const { id, status: fromStatus } = JSON.parse(data);

    const col = this.columns().find(c => c.status === fromStatus);
    if (!col) return;
    const app = col.items.find(a => a.id === id);
    if (!app || fromStatus === targetColumn) return;

    // Optimistic update
    this.columns.update(cols =>
      cols.map(c => {
        if (c.status === fromStatus) return { ...c, items: c.items.filter(a => a.id !== id) };
        if (c.status === targetColumn) return { ...c, items: [{ ...app, status: targetColumn }, ...c.items] };
        return c;
      })
    );

    try {
      await this.appService.updateStatus(id, { status: targetColumn });
    } catch {
      // Rollback
      this.loadApps();
    }
  }

  logoBg(name: string): string {
    const colors = ['#E6F1FB', '#EEEDFE', '#FAEEDA', '#E1F5EE', '#F1EFE8', '#FCEBEB'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return colors[Math.abs(hash) % colors.length];
  }

  getStatusLabel(s: string): string { return STATUS_LABELS[s as ApplicationStatus] || s; }
  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }
}
