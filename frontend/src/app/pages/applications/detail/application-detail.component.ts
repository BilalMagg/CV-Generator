import { Component, signal, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApplicationService } from '../../../services/application.service';
import {
  ApplicationResponseDto,
  ApplicationStatus,
  STATUS_LABELS,
} from '../../../models/application.model';

const NEXT_STATUSES: Record<ApplicationStatus, ApplicationStatus[]> = {
  PENDING: ['REVIEWED', 'CANCELLED'],
  REVIEWED: ['INTERVIEW', 'REJECTED', 'CANCELLED'],
  INTERVIEW: ['ACCEPTED', 'REJECTED', 'CANCELLED'],
  ACCEPTED: [],
  REJECTED: ['INTERVIEW'],
  CANCELLED: ['PENDING'],
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

  availableStatuses = computed(() => NEXT_STATUSES[this.application()?.status || 'PENDING'] || []);

  protected readonly STATUS_LABELS = STATUS_LABELS;

  ngOnInit() {
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
        this.error.set(res.message || 'Failed to load');
      }
    } catch { this.error.set('Failed to load'); }
    finally { this.loading.set(false); }
  }

  resetEditForm(app: ApplicationResponseDto) {
    this.editCompanyName.set(app.companyName);
    this.editPositionTitle.set(app.positionTitle);
    this.editOfferSource.set(app.offerSource || '');
    this.editNotes.set(app.notes || '');
  }

  openStatusModal() { this.newStatus.set(this.availableStatuses()[0] || 'PENDING'); this.statusComment.set(''); this.showStatusModal.set(true); }
  closeStatusModal() { this.showStatusModal.set(false); }
  openEditModal() { const app = this.application(); if (app) this.resetEditForm(app); this.showEditModal.set(true); }
  closeEditModal() { this.showEditModal.set(false); }

  async updateStatus() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.updateStatus(app.id, { status: this.newStatus(), comment: this.statusComment() || undefined });
      if (res.success && res.data) this.application.set(res.data);
      this.closeStatusModal();
    } catch { } finally { this.saving.set(false); }
  }

  async saveEdit() {
    const app = this.application();
    if (!app) return;
    this.saving.set(true);
    try {
      const res = await this.appService.update(app.id, {
        companyName: this.editCompanyName(), positionTitle: this.editPositionTitle(),
        offerSource: this.editOfferSource() || undefined, notes: this.editNotes() || undefined,
      });
      if (res.success && res.data) this.application.set(res.data);
      this.closeEditModal();
    } catch { } finally { this.saving.set(false); }
  }

  async onDelete() {
    const app = this.application();
    if (!app || !confirm('Delete this application?')) return;
    try { await this.appService.delete(app.id); this.router.navigate(['/applications/list']); }
    catch { }
  }

  getStatusLabel(s: string) { return STATUS_LABELS[s as ApplicationStatus] || s; }
  formatDate(d: string) {
    return new Date(d).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' });
  }

  statusBadgeClass(s: string) { return `badge-lg b-${s.toLowerCase()}`; }
  statusDotColor(s: string): string {
    const map: Record<string, string> = {
      pending: '#888780', reviewed: '#378ADD', interview: '#7F77DD',
      accepted: '#639922', rejected: '#E24B4A', cancelled: '#888780',
    };
    return map[s.toLowerCase()] || '#888780';
  }
}
