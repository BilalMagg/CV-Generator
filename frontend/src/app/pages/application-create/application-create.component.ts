import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApplicationService } from '../../services/application.service';
import { AuthService } from '../../services/auth.service';
import {
  ApplicationStatus,
  STATUS_LABELS,
} from '../../models/application.model';

@Component({
  selector: 'app-application-create',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './application-create.component.html',
  styleUrl: './application-create.component.scss',
})
export class ApplicationCreateComponent {
  private appService = inject(ApplicationService);
  private authService = inject(AuthService);
  private router = inject(Router);

  submitting = signal(false);
  error = signal<string | null>(null);

  form = signal({
    companyName: '',
    positionTitle: '',
    offerSource: '',
    notes: '',
  });

  formErrors = signal<Record<string, string>>({});

  protected readonly STATUS_LABELS = STATUS_LABELS;

  get isFormValid(): boolean {
    const f = this.form();
    return f.companyName.trim().length > 0 && f.positionTitle.trim().length > 0;
  }

  validate(): boolean {
    const f = this.form();
    const errors: Record<string, string> = {};
    if (!f.companyName.trim()) errors['companyName'] = 'Company name is required';
    if (!f.positionTitle.trim()) errors['positionTitle'] = 'Position title is required';
    this.formErrors.set(errors);
    return Object.keys(errors).length === 0;
  }

  async onSubmit() {
    if (!this.validate()) return;
    this.submitting.set(true);
    this.error.set(null);

    const user = this.authService.currentUser();
    if (!user) {
      this.error.set('You must be logged in to create an application');
      this.submitting.set(false);
      return;
    }

    const f = this.form();
    try {
      const res = await this.appService.create({
        candidateId: user.userId,
        companyName: f.companyName.trim(),
        positionTitle: f.positionTitle.trim(),
        offerSource: f.offerSource.trim() || undefined,
        notes: f.notes.trim() || undefined,
      });

      if (res.success && res.data) {
        this.router.navigate(['/applications', res.data.id]);
      } else {
        this.error.set('Failed to create application');
      }
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'An unexpected error occurred');
    } finally {
      this.submitting.set(false);
    }
  }
}
