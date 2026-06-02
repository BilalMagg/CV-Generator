import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '@app/services/auth.service';
import { APP_NAME } from '@app/app-name';

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

  // Sign-in
  signInEmail = '';
  signInPassword = '';
  signInLoading = false;
  signInError = '';

  // Sign-up
  signUpFirstName = '';
  signUpLastName = '';
  signUpEmail = '';
  signUpPassword = '';
  signUpConfirm = '';
  signUpLoading = false;
  signUpError = '';
  signUpSuccess = '';

  toggleMode(): void {
    this.mode.update(m => m === 'sign-in' ? 'sign-up' : 'sign-in');
    this.signInError = '';
    this.signUpError = '';
    this.signUpSuccess = '';
  }

  async onSignIn(): Promise<void> {
    this.signInError = '';
    if (!this.signInEmail || !this.signInPassword) {
      this.signInError = 'Please enter your email and password';
      return;
    }
    this.signInLoading = true;
    try {
      await this.authService.loginWithCredentials(this.signInEmail, this.signInPassword);
      document.body.classList.remove('page-revealed');
      window.location.href = '/applications';
    } catch (err) {
      this.signInError = err instanceof Error ? err.message : 'Login failed';
      this.signInLoading = false;
    }
  }

  async onSignUp(): Promise<void> {
    this.signUpError = '';
    this.signUpSuccess = '';
    if (!this.signUpFirstName || !this.signUpLastName || !this.signUpEmail || !this.signUpPassword) {
      this.signUpError = 'All fields are required';
      return;
    }
    if (this.signUpPassword.length < 8) {
      this.signUpError = 'Password must be at least 8 characters';
      return;
    }
    if (this.signUpPassword !== this.signUpConfirm) {
      this.signUpError = 'Passwords do not match';
      return;
    }
    this.signUpLoading = true;
    try {
      const result = await this.authService.register({
        firstName: this.signUpFirstName,
        lastName: this.signUpLastName,
        email: this.signUpEmail,
        password: this.signUpPassword,
      });
      if (result.success) {
        this.signUpSuccess = 'Account created! Redirecting to login...';
        setTimeout(() => this.mode.set('sign-in'), 1500);
      } else {
        this.signUpError = result.message || 'Registration failed';
      }
    } catch (err) {
      this.signUpError = err instanceof Error ? err.message : 'Registration failed';
    } finally {
      this.signUpLoading = false;
    }
  }

  signInWithSso(): void {
    this.authService.loginWithSso();
  }

  signInWithProvider(provider: string): void {
    this.authService.loginWithSso(provider);
  }
}
