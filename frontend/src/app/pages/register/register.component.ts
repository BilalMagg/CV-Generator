import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpService } from '../../services/http.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss',
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
