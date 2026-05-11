import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpService } from '../../services/http.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="container">
      <div class="card">
        <h1>Create Account</h1>
        <p>Register to get started with CV Generator.</p>

        @if (error()) {
          <div class="alert error">{{ error() }}</div>
        }

        @if (success()) {
          <div class="alert success">{{ success() }}</div>
        }

        <form (ngSubmit)="onSubmit()" #registerForm="ngForm">
          <div class="form-group">
            <label for="firstName">First Name</label>
            <input
              type="text"
              id="firstName"
              name="firstName"
              [(ngModel)]="firstName"
              #firstNameRef="ngModel"
              required
              placeholder="John"
              [class.input-error]="firstNameRef.invalid && firstNameRef.dirty"
            />
            @if (firstNameRef.invalid && firstNameRef.dirty) {
              <span class="field-error">First name is required</span>
            }
          </div>

          <div class="form-group">
            <label for="lastName">Last Name</label>
            <input
              type="text"
              id="lastName"
              name="lastName"
              [(ngModel)]="lastName"
              #lastNameRef="ngModel"
              required
              placeholder="Doe"
              [class.input-error]="lastNameRef.invalid && lastNameRef.dirty"
            />
            @if (lastNameRef.invalid && lastNameRef.dirty) {
              <span class="field-error">Last name is required</span>
            }
          </div>

          <div class="form-group">
            <label for="email">Email</label>
            <input
              type="email"
              id="email"
              name="email"
              [(ngModel)]="email"
              #emailRef="ngModel"
              required
              email
              placeholder="john.doe@example.com"
              [class.input-error]="emailRef.invalid && emailRef.dirty"
            />
            @if (emailRef.invalid && emailRef.dirty) {
              @if (emailRef.errors?.['required']) {
                <span class="field-error">Email is required</span>
              } @else {
                <span class="field-error">Enter a valid email address</span>
              }
            }
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              name="password"
              [(ngModel)]="password"
              #passwordRef="ngModel"
              required
              minlength="8"
              placeholder="Minimum 8 characters"
              [class.input-error]="passwordRef.invalid && passwordRef.dirty"
            />
            @if (passwordRef.invalid && passwordRef.dirty) {
              @if (passwordRef.errors?.['required']) {
                <span class="field-error">Password is required</span>
              } @else {
                <span class="field-error">At least 8 characters</span>
              }
            }
          </div>

          <div class="form-group">
            <label for="confirmPassword">Confirm Password</label>
            <input
              type="password"
              id="confirmPassword"
              name="confirmPassword"
              [(ngModel)]="confirmPassword"
              #confirmRef="ngModel"
              required
              placeholder="Repeat your password"
              [class.input-error]="confirmRef.invalid && confirmRef.dirty"
            />
            @if (confirmRef.invalid && confirmRef.dirty && confirmRef.errors?.['required']) {
              <span class="field-error">Please confirm your password</span>
            }
          </div>

          <button type="submit" [disabled]="loading()" class="btn btn-primary">
            @if (loading()) {
              <span class="spinner"></span> Creating Account...
            } @else {
              Create Account
            }
          </button>
        </form>

        <div class="links">
          <a routerLink="/login">Already have an account? Login</a>
          <a routerLink="/">Back to Home</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .container {
      min-height: 100vh;
      display: flex;
      justify-content: center;
      align-items: center;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 2rem;
    }
    .card {
      background: white;
      padding: 2.5rem;
      border-radius: 8px;
      box-shadow: 0 4px 20px rgba(0,0,0,0.2);
      max-width: 450px;
      width: 100%;
    }
    h1 { margin: 0 0 0.5rem; color: #333; text-align: center; }
    p { color: #666; text-align: center; margin-bottom: 1.5rem; }
    .alert {
      padding: 0.75rem;
      border-radius: 4px;
      margin-bottom: 1rem;
      font-size: 0.9rem;
    }
    .alert.error { background: #f8d7da; color: #721c24; }
    .alert.success { background: #d4edda; color: #155724; }
    .form-group { margin-bottom: 1rem; }
    label {
      display: block;
      margin-bottom: 0.25rem;
      font-weight: 500;
      color: #333;
    }
    input {
      width: 100%;
      padding: 0.75rem;
      border: 1px solid #ddd;
      border-radius: 4px;
      font-size: 1rem;
      box-sizing: border-box;
      transition: border-color 0.2s;
    }
    input:focus { outline: none; border-color: #667eea; }
    input.input-error { border-color: #dc3545; }
    .field-error {
      display: block;
      color: #dc3545;
      font-size: 0.8rem;
      margin-top: 0.25rem;
    }
    .btn {
      width: 100%;
      padding: 0.875rem;
      border: none;
      border-radius: 4px;
      font-weight: 500;
      cursor: pointer;
      margin-top: 0.5rem;
      font-size: 1rem;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
    }
    .btn-primary { background: #667eea; color: white; }
    .btn-primary:hover:not(:disabled) { background: #5a6fd6; }
    .btn-primary:disabled { background: #ccc; cursor: not-allowed; }
    .spinner {
      display: inline-block;
      width: 1rem;
      height: 1rem;
      border: 2px solid rgba(255,255,255,0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.6s linear infinite;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
    .links {
      margin-top: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      text-align: center;
    }
    .links a {
      color: #667eea;
      text-decoration: none;
      font-size: 0.9rem;
    }
    .links a:hover { text-decoration: underline; }
  `]
})
export class RegisterComponent {
  firstName = '';
  lastName = '';
  email = '';
  password = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');
  success = signal('');

  constructor(
    private http: HttpService,
    private router: Router
  ) {}

  async onSubmit(): Promise<void> {
    this.error.set('');

    if (!this.firstName || !this.lastName || !this.email || !this.password) {
      this.error.set('All fields are required');
      return;
    }

    if (this.password.length < 8) {
      this.error.set('Password must be at least 8 characters');
      return;
    }

    if (this.password !== this.confirmPassword) {
      this.error.set('Passwords do not match');
      return;
    }

    this.loading.set(true);
    this.success.set('');

    try {
      const response = await this.http.post<{success: boolean; message?: string}>('/api/auth/register', {
        firstName: this.firstName,
        lastName: this.lastName,
        email: this.email,
        password: this.password,
      });

      if (response.success) {
        this.success.set('Account created successfully! Redirecting to login...');
        setTimeout(() => this.router.navigate(['/login']), 2000);
      } else {
        this.error.set(response.message || 'Registration failed');
      }
    } catch (err: any) {
      if (err?.status === 409) {
        this.error.set('An account with this email already exists');
      } else if (err?.error?.message) {
        this.error.set(err.error.message);
      } else {
        this.error.set('Registration failed. Please try again.');
      }
    } finally {
      this.loading.set(false);
    }
  }
}
