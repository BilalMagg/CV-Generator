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

  showPassword = signal(false);
  togglePassword(): void { this.showPassword.update(v => !v); }

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
}
