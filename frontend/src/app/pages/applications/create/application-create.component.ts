import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApplicationService } from '../../../services/application.service';
import { AuthService } from '../../../services/auth.service';

@Component({
  selector: 'app-application-create',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './application-create.component.html',
  styleUrl: './application-create.component.scss',
})
export class ApplicationCreateComponent {
  private appService = inject(ApplicationService);
  private authService = inject(AuthService);
  protected router = inject(Router);

  submitting = signal(false);
  error = signal<string | null>(null);

  companyName = signal('');
  positionTitle = signal('');
  offerSource = signal('');
  notes = signal('');
  priority = signal<'low' | 'medium' | 'high' | 'urgent'>('medium');

  get isFormValid(): boolean {
    return this.companyName().trim().length > 0 && this.positionTitle().trim().length > 0;
  }

  setPriority(p: 'low' | 'medium' | 'high' | 'urgent') { this.priority.set(p); }

  async onSubmit() {
    if (!this.isFormValid) return;
    this.submitting.set(true);
    this.error.set(null);

    const user = this.authService.currentUser();
    if (!user) {
      this.error.set('You must be logged in');
      this.submitting.set(false);
      return;
    }

    try {
      const res = await this.appService.create({
        candidateId: user.userId,
        companyName: this.companyName().trim(),
        positionTitle: this.positionTitle().trim(),
        offerSource: this.offerSource().trim() || undefined,
        notes: this.notes().trim() || undefined,
      });
      if (res.success && res.data) {
        this.router.navigate(['/applications', res.data.id]);
      } else {
        this.error.set(res.message || 'Failed to create');
      }
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'An error occurred');
    } finally {
      this.submitting.set(false);
    }
  }
}
