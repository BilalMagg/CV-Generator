import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@app/services/auth.service';
import { APP_NAME } from '@app/app-name';
import { extractError } from '@app/shared/error-utils';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);

  appName = APP_NAME;

  mode = signal<'sign-in' | 'sign-up'>(
    (this.route.snapshot.queryParamMap.get('mode') as 'sign-in' | 'sign-up') || 'sign-in'
  );

  signInEmail = '';
  signInPassword = '';
  signInLoading = signal(false);
  signInError = signal('');

  signUpFirstName = '';
  signUpLastName = '';
  signUpEmail = '';
  signUpPassword = '';
  signUpConfirm = '';
  signUpLoading = signal(false);
  signUpError = signal('');
  signUpSuccess = signal('');

  resetVisible = signal(false);
  resetEmail = '';
  resetPassword = '';
  resetConfirm = '';
  resetLoading = signal(false);
  resetError = signal('');
  resetSuccess = signal('');

  toggleMode(): void {
    this.mode.update(m => m === 'sign-in' ? 'sign-up' : 'sign-in');
    this.signInError.set('');
    this.signUpError.set('');
    this.signUpSuccess.set('');
  }

  async onSignIn(): Promise<void> {
    this.signInError.set('');
    if (!this.signInEmail || !this.signInPassword) {
      this.signInError.set('Please enter your email and password');
      return;
    }
    this.signInLoading.set(true);
    try {
      await this.authService.loginWithCredentials(this.signInEmail, this.signInPassword);
      document.body.classList.remove('page-revealed');
      window.location.href = '/applications';
    } catch (err) {
      this.signInError.set(extractError(err));
    } finally {
      this.signInLoading.set(false);
    }
  }

  async onSignUp(): Promise<void> {
    this.signUpError.set('');
    this.signUpSuccess.set('');
    if (!this.signUpFirstName || !this.signUpLastName || !this.signUpEmail || !this.signUpPassword) {
      this.signUpError.set('All fields are required');
      return;
    }
    if (this.signUpPassword.length < 8) {
      this.signUpError.set('Password must be at least 8 characters');
      return;
    }
    if (this.signUpPassword !== this.signUpConfirm) {
      this.signUpError.set('Passwords do not match');
      return;
    }
    this.signUpLoading.set(true);
    try {
      const result = await this.authService.register({
        firstName: this.signUpFirstName,
        lastName: this.signUpLastName,
        email: this.signUpEmail,
        password: this.signUpPassword,
      });
      if (result.success) {
        this.signUpSuccess.set('Account created! Redirecting to login...');
        setTimeout(() => this.mode.set('sign-in'), 1500);
      } else {
        this.signUpError.set(result.message || 'Registration failed');
      }
    } catch (err) {
      this.signUpError.set(err instanceof Error ? err.message : 'Registration failed');
    } finally {
      this.signUpLoading.set(false);
    }
  }

  signInWithSso(): void {
    this.authService.loginWithSso();
  }

  signInWithProvider(provider: string): void {
    this.authService.loginWithSso(provider);
  }

  showReset(): void {
    this.resetVisible.set(true);
    this.resetEmail = this.signInEmail;
    this.resetError.set('');
    this.resetSuccess.set('');
  }

  hideReset(): void {
    this.resetVisible.set(false);
    this.resetError.set('');
    this.resetSuccess.set('');
  }

  async onResetPassword(): Promise<void> {
    this.resetError.set('');
    this.resetSuccess.set('');
    if (!this.resetEmail.trim() || !this.resetPassword || !this.resetConfirm) {
      this.resetError.set('All fields are required');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.resetEmail.trim())) {
      this.resetError.set('Please enter a valid email address');
      return;
    }
    if (this.resetPassword.length < 8) {
      this.resetError.set('Password must be at least 8 characters');
      return;
    }
    if (this.resetPassword !== this.resetConfirm) {
      this.resetError.set('Passwords do not match');
      return;
    }
    this.resetLoading.set(true);
    try {
      const result = await this.authService.resetPassword({
        email: this.resetEmail.trim(),
        password: this.resetPassword,
      });
      if (result.success) {
        this.resetSuccess.set(result.message);
        this.signInEmail = this.resetEmail.trim();
        this.resetPassword = '';
        this.resetConfirm = '';
        setTimeout(() => this.hideReset(), 2500);
      } else {
        this.resetError.set(result.message);
      }
    } catch (err) {
      this.resetError.set(err instanceof Error ? err.message : 'Password reset failed');
    } finally {
      this.resetLoading.set(false);
    }
  }
}
