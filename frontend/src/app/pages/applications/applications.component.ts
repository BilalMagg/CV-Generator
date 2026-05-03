import {
  Component,
  signal,
  computed,
  inject,
  OnInit,
  DestroyRef,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApplicationService } from '../../services/application.service';
import {
  ApplicationResponseDto,
  ApplicationStatisticsDto,
  ApplicationStatus,
  STATUS_LABELS,
  STATUS_ORDER,
} from '../../models/application.model';

interface StatsCard {
  status: ApplicationStatus;
  label: string;
  count: number;
  color: string;
  bg: string;
}

const STATUS_COLORS: Record<ApplicationStatus, { color: string; bg: string }> =
  {
    PENDING: { color: '#f59e0b', bg: '#fef3c7' },
    REVIEWED: { color: '#3b82f6', bg: '#dbeafe' },
    INTERVIEW: { color: '#8b5cf6', bg: '#ede9fe' },
    ACCEPTED: { color: '#10b981', bg: '#d1fae5' },
    REJECTED: { color: '#ef4444', bg: '#fee2e2' },
    CANCELLED: { color: '#6b7280', bg: '#f3f4f6' },
  };

@Component({
  selector: 'app-applications',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './applications.component.html',
  styleUrl: './applications.component.scss',
})
export class ApplicationsComponent implements OnInit {
  private appService = inject(ApplicationService);
  private destroyRef = inject(DestroyRef);

  applications = signal<ApplicationResponseDto[]>([]);
  statistics = signal<ApplicationStatisticsDto>({
    total: 0,
    pending: 0,
    reviewed: 0,
    interview: 0,
    accepted: 0,
    rejected: 0,
    cancelled: 0,
  });
  loading = signal(true);
  page = signal(1);
  pageSize = signal(15);
  totalItems = signal(0);
  searchQuery = signal('');
  selectedStatus = signal<ApplicationStatus | ''>('');

  statsCards = computed<StatsCard[]>(() => {
    const s = this.statistics();
    return STATUS_ORDER.map((status) => ({
      status,
      label: STATUS_LABELS[status],
      count: s[status.toLowerCase() as keyof Omit<ApplicationStatisticsDto, 'total'>],
      color: STATUS_COLORS[status].color,
      bg: STATUS_COLORS[status].bg,
    }));
  });

  totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalItems() / this.pageSize())),
  );

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

  ngOnInit(): void {
    this.loadData();
  }

  async loadData() {
    this.loading.set(true);
    try {
      const [listRes, statsRes] = await Promise.all([
        this.appService.getAll({
          page: this.page(),
          pageSize: this.pageSize(),
          status: this.selectedStatus() || undefined,
          search: this.searchQuery() || undefined,
        }),
        this.appService.getStatistics(),
      ]);
      if (listRes.success && listRes.data) {
        this.applications.set(listRes.data.items);
        this.totalItems.set(listRes.data.total);
      }
      if (statsRes.success && statsRes.data) {
        this.statistics.set(statsRes.data);
      }
    } catch (err) {
      console.error('Failed to load applications:', err);
    } finally {
      this.loading.set(false);
    }
  }

  onSearch() {
    this.page.set(1);
    this.loadData();
  }

  onStatusFilterChange() {
    this.page.set(1);
    this.loadData();
  }

  changePage(p: number) {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.loadData();
  }

  async onDelete(id: string) {
    if (!confirm('Delete this application?')) return;
    try {
      await this.appService.delete(id);
      this.loadData();
    } catch (err) {
      console.error('Failed to delete:', err);
    }
  }

  statusBadgeClass(status: ApplicationStatus): string {
    return `badge badge-${status.toLowerCase()}`;
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
    });
  }

  getStatusLabel(status: string): string {
    return STATUS_LABELS[status as ApplicationStatus];
  }
}
