import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UserProfileService } from '../../services/user-profile.service';
import { AuthService } from '../../services/auth.service';
import { UserProfile, UpdateUserProfileDto } from '../../models/user-profile.model';

@Component({
  selector: 'app-personal-info',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './personal-info.component.html',
  styleUrl: './personal-info.component.scss',
})
export class PersonalInfoComponent implements OnInit {
  private profileSvc = inject(UserProfileService);
  private authSvc = inject(AuthService);

  profile = signal<UserProfile | null>(null);
  loading = signal(true);
  saving = signal(false);
  saved = signal(false);
  error = signal<string | null>(null);

  ngOnInit() {
    this.loadProfile();
  }

  async loadProfile() {
    this.loading.set(true);
    try {
      const profile = await this.profileSvc.getMyProfile();
      this.profile.set(profile);
      if (profile) {
        this.authSvc.currentUser.set({
          userId: profile.id,
          keycloakId: profile.keycloakId,
          firstName: profile.firstName,
          lastName: profile.lastName,
          email: profile.email,
          role: profile.role,
          isActive: profile.isActive,
        });
      }
    } catch {
      this.error.set('Failed to load profile');
    } finally {
      this.loading.set(false);
    }
  }

  async save() {
    const p = this.profile();
    if (!p) return;

    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);

    try {
      const dto: UpdateUserProfileDto = {
        firstName: p.firstName,
        lastName: p.lastName,
        phoneNumber: p.phoneNumber,
        birthDate: p.birthDate,
        avatarUrl: p.avatarUrl,
        preferencesJson: p.preferencesJson,
      };

      const updated = await this.profileSvc.updateProfile(p.id, dto);
      if (updated) {
        this.profile.set(updated);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 2500);
      }
    } catch {
      this.error.set('Failed to save profile');
    } finally {
      this.saving.set(false);
    }
  }

  formatDate(iso: string | undefined): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-US', {
      year: 'numeric', month: 'long', day: 'numeric',
    });
  }
}
