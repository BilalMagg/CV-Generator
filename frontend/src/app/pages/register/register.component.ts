import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpService } from '../../services/http.service';
import { environment } from '../../../environments/environment';

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
          <div class="error">{{ error() }}</div>
        }

        @if (success()) {
          <div class="success">{{ success() }}</div>
        }

        <form (ngSubmit)="onSubmit()" #registerForm="ngForm">
          <div class="form-group">
            <label for="firstName">First Name</label>
            <input
              type="text"
              id="firstName"
              name="firstName"
              [(ngModel)]="firstName"
              required
              placeholder="John"
            />
          </div>

          <div class="form-group">
            <label for="lastName">Last Name</label>
            <input
              type="text"
              id="lastName"
              name="lastName"
              [(ngModel)]="lastName"
              required
              placeholder="Doe"
            />
          </div>

          <div class="form-group">
            <label for="email">Email</label>
            <input
              type="email"
              id="email"
              name="email"
              [(ngModel)]="email"
              required
              placeholder="john.doe@example.com"
            />
          </div>

          <div class="form-group">
            <label for="password">Password</label>
            <input
              type="password"
              id="password"
              name="password"
              [(ngModel)]="password"
              required
              minlength="8"
              placeholder="Minimum 8 characters"
            />
          </div>

          <button type="submit" [disabled]="loading()" class="btn btn-primary">
            {{ loading() ? 'Creating Account...' : 'Register' }}
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
    .error {
      background: #f8d7da;
      color: #721c24;
      padding: 0.75rem;
      border-radius: 4px;
      margin-bottom: 1rem;
      font-size: 0.9rem;
    }
    .success {
      background: #d4edda;
      color: #155724;
      padding: 0.75rem;
      border-radius: 4px;
      margin-bottom: 1rem;
      font-size: 0.9rem;
    }
    .form-group {
      margin-bottom: 1rem;
    }
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
    }
    input:focus {
      outline: none;
      border-color: #667eea;
    }
    .btn {
      width: 100%;
      padding: 0.875rem;
      border: none;
      border-radius: 4px;
      font-weight: 500;
      cursor: pointer;
      margin-top: 0.5rem;
    }
    .btn-primary {
      background: #667eea;
      color: white;
    }
    .btn-primary:hover:not(:disabled) {
      background: #5a6fd6;
    }
    .btn-primary:disabled {
      background: #ccc;
      cursor: not-allowed;
    }
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
    .links a:hover {
      text-decoration: underline;
    }
  `]
})
export class RegisterComponent {
  firstName = '';
  lastName = '';
  email = '';
  password = '';
  loading = signal(false);
  error = signal('');
  success = signal('');

  constructor(
    private http: HttpService,
    private router: Router
  ) {}

  async onSubmit(): Promise<void> {
    if (!this.firstName || !this.lastName || !this.email || !this.password) {
      this.error.set('All fields are required');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.success.set('');

    try {
      const response = await fetch(`${environment.apiUrl}/api/auth/register`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        credentials: 'include',
        body: JSON.stringify({
          firstName: this.firstName,
          lastName: this.lastName,
          email: this.email,
          password: this.password
        })
      });

      const data = await response.json();

      if (response.ok) {
        this.success.set('Account created successfully! Redirecting to login...');
        setTimeout(() => this.router.navigate(['/login']), 2000);
      } else {
        this.error.set(data.message || data.errors || 'Registration failed');
      }
    } catch (err) {
      this.error.set('An error occurred. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }
}
