import {
  Component,
  signal,
  inject,
  OnInit,
  computed,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApplicationService } from '../../services/application.service';
import {
  ApplicationResponseDto,
  StatusHistoryDto,
  ApplicationStatus,
  STATUS_LABELS,
  STATUS_ORDER,
} from '../../models/application.model';

const NEXT_STATUSES: Record<ApplicationStatus, ApplicationStatus[]> = {
  PENDING: ['REVIEWED', 'CANCELLED'],
  REVIEWED: ['INTERVIEW', 'REJECTED', 'CANCELLED'],
  INTERVIEW: ['ACCEPTED', 'REJECTED', 'CANCELLED'],
  ACCEPTED: [],
  REJECTED: ['INTERVIEW'],
  CANCELLED: ['PENDING'],
};

const STATUS_COLORS: Record<ApplicationStatus, string> = {
  PENDING: '#f59e0b',
  REVIEWED: '#3b82f6',
  INTERVIEW: '#8b5cf6',
  ACCEPTED: '#10b981',
  REJECTED: '#ef4444',
  CANCELLED: '#6b7280',
};

@Component({
  selector: 'app-application-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './application-detail.component.html',
  styleUrl: './application-detail.component.scss',
})
export class ApplicationDetailComponent implements OnInit {
  private appService = inject(ApplicationService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  application = signal<ApplicationResponseDto | null>(null);
  loading = signal(true);
  saving = signal(false);
  error = signal<string | null>(null);

  showStatusModal = signal(false);
  showEditModal = signal(false);

  newStatus = signal<ApplicationStatus>('PENDING');
  statusComment = signal('');

  editCompanyName = signal('');
  editPositionTitle = signal('');
  editOfferSource = signal('');
  editNotes = signal('');

  availableStatuses = computed(() => {
    const app = this.application();
    if (!app) return [];
    return NEXT_STATUSES[app.status] || [];
  });

  history = computed(() => this.application()?.history ?? []);
  notes = computed(() => this.application()?.notes ?? '');
  offerSource = computed(() => this.application()?.offerSource ?? '');

  statusColor = computed(() => {
    const app = this.application();
    return app ? STATUS_COLORS[app.status] : '#6b7280';
  });

  protected readonly STATUS_COLORS = STATUS_COLORS;

  protected readonly STATUS_LABELS = STATUS_LABELS;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.loadApplication(id);
  }

  async loadApplication(id: string) {
    this.loading.set(true);
    try {
      const res = await this.appService.getById(id);
      if (res.success && res.data) {
        this.application.set(res.data);
        this.resetEditForm(res.data);
      } else {
        this.error.set('Failed to load application');
      }
    } catch {
      this.error.set('Failed to load application');
    } finally {
      this.loading.set(false);
    }
  }

  resetEditForm(app: ApplicationResponseDto) {
    this.editCompanyName.set(app.companyName);
    this.editPositionTitle.set(app.positionTitle);
    this.editOfferSource.set(app.offerSource || '');
    this.editNotes.set(app.notes || '');
  }

  openStatusModal() {
    this.newStatus.set(this.availableStatuses()[0] || 'PENDING');
    this.statusComment.set('');
    this.showStatusModal.set(true);
  }

  closeStatusModal() {
    this.showStatusModal.set(false);
  }

  openEditModal() {
    const app = this.application();
    if (app) this.resetEditForm(app);
    this.showEditModal.set(true);
  }

  closeEditModal() {
    this.showEditModal.set(false);
  }

  async updateStatus() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.updateStatus(app.id, {
        status: this.newStatus(),
        comment: this.statusComment() || undefined,
      });
      if (res.success && res.data) {
        this.application.set(res.data);
      }
      this.closeStatusModal();
    } catch (err) {
      console.error('Failed to update status:', err);
    } finally {
      this.saving.set(false);
    }
  }

  async saveEdit() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.update(app.id, {
        companyName: this.editCompanyName(),
        positionTitle: this.editPositionTitle(),
        offerSource: this.editOfferSource() || undefined,
        notes: this.editNotes() || undefined,
      });
      if (res.success && res.data) {
        this.application.set(res.data);
      }
      this.closeEditModal();
    } catch (err) {
      console.error('Failed to update:', err);
    } finally {
      this.saving.set(false);
    }
  }

  async onDelete() {
    const app = this.application();
    if (!app) return;
    if (!confirm('Delete this application?')) return;
    try {
      await this.appService.delete(app.id);
      this.router.navigate(['/applications']);
    } catch (err) {
      console.error('Failed to delete:', err);
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  }

  statusBadgeClass(status: string): string {
    return `status-badge status-${status.toLowerCase()}`;
  }

  getStatusLabel(status: string): string {
    return STATUS_LABELS[status as ApplicationStatus] || status;
  }

  getStatusColor(status: string): string {
    return STATUS_COLORS[status as ApplicationStatus] || '#6b7280';
  }
}
