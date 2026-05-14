import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink, Router } from '@angular/router';
import { ApplicationService } from '../../../services/application.service';
import {
  ApplicationResponseDto,
  ApplicationStatisticsDto,
  ApplicationStatus,
  STATUS_LABELS,
  STATUS_ORDER,
} from '../../../models/application.model';

interface StatsCard {
  status: ApplicationStatus;
  label: string;
  count: number;
}

const STATUS_BG: Record<string, string> = {
  pending: '#F1EFE8', reviewed: '#E6F1FB', interview: '#EEEDFE',
  accepted: '#EAF3DE', rejected: '#FCEBEB', cancelled: '#F1EFE8',
};
const STATUS_TEXT: Record<string, string> = {
  pending: '#5F5E5A', reviewed: '#185FA5', interview: '#534AB7',
  accepted: '#3B6D11', rejected: '#A32D2D', cancelled: '#888780',
};

@Component({
  selector: 'app-applications-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './applications-list.component.html',
  styleUrl: './applications-list.component.scss',
})
export class ApplicationsListComponent implements OnInit {
  private appService = inject(ApplicationService);
  protected router = inject(Router);

  applications = signal<ApplicationResponseDto[]>([]);
  statistics = signal<ApplicationStatisticsDto>({ total: 0, pending: 0, reviewed: 0, interview: 0, accepted: 0, rejected: 0, cancelled: 0 });
  loading = signal(true);
  page = signal(1);
  pageSize = signal(15);
  totalItems = signal(0);
  searchQuery = signal('');
  selectedStatus = signal<ApplicationStatus | ''>('');

  totalPages = computed(() => Math.max(1, Math.ceil(this.totalItems() / this.pageSize())));
  visiblePages = computed(() => {
    const current = this.page();
    const total = this.totalPages();
    const pages: number[] = [];
    const start = Math.max(1, current - 2);
    const end = Math.min(total, current + 2);
    for (let i = start; i <= end; i++) pages.push(i);
    return pages;
  });

  protected readonly STATUS_LABELS = STATUS_LABELS;
  protected readonly STATUS_BG = STATUS_BG;
  protected readonly STATUS_TEXT = STATUS_TEXT;
  protected Math = Math;

  ngOnInit() { this.loadData(); }

  async loadData() {
    this.loading.set(true);
    try {
      const [listRes, statsRes] = await Promise.all([
        this.appService.getAll({
          page: this.page(), pageSize: this.pageSize(),
          status: this.selectedStatus() || undefined,
          search: this.searchQuery() || undefined,
        }),
        this.appService.getStatistics(),
      ]);
      if (listRes.success && listRes.data) {
        this.applications.set(listRes.data.items);
        this.totalItems.set(listRes.data.total);
      }
      if (statsRes.success && statsRes.data) this.statistics.set(statsRes.data);
    } catch (err) { console.error(err); }
    finally { this.loading.set(false); }
  }

  onSearch() { this.page.set(1); this.loadData(); }
  onStatusFilterChange() { this.page.set(1); this.loadData(); }
  changePage(p: number) { if (p >= 1 && p <= this.totalPages()) { this.page.set(p); this.loadData(); } }

  async onDelete(id: string) {
    if (!confirm('Delete this application?')) return;
    try { await this.appService.delete(id); this.loadData(); }
    catch { }
  }

  async onInlineStatusChange(app: ApplicationResponseDto, event: Event) {
    const newStatus = (event.target as HTMLSelectElement).value as ApplicationStatus;
    try {
      const res = await this.appService.updateStatus(app.id, { status: newStatus });
      if (res.success) this.loadData();
    } catch { }
  }

  getStatusLabel(s: string) { return STATUS_LABELS[s as ApplicationStatus] || s; }
  formatDate(d: string) {
    return new Date(d).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  }
  logoBg(name: string): string {
    const colors = ['#E6F1FB', '#EEEDFE', '#FAEEDA', '#E1F5EE', '#F1EFE8', '#FCEBEB', '#FAECE7'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return colors[Math.abs(hash) % colors.length];
  }
  logoColor(name: string): string {
    const colors = ['#185FA5', '#534AB7', '#854F0B', '#0F6E56', '#5F5E5A', '#A32D2D', '#993C1D'];
    let hash = 0;
    for (let i = 0; i < name.length; i++) hash = name.charCodeAt(i) + ((hash << 5) - hash);
    return colors[Math.abs(hash) % colors.length];
  }
}
