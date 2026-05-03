import { Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="container">
      <div class="card">
        <h1>Login</h1>
        <p>Sign in to access your CV Generator account.</p>

        @if (!isTempAuth()) {
          <a [href]="loginUrl()" class="btn btn-primary">Continue with Keycloak</a>
        } @else {
          <button class="btn btn-primary" (click)="loginWithTemp()">Use Demo Account</button>
        }

        <div class="links">
          <a routerLink="/register">Don't have an account? Register</a>
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
      text-align: center;
      max-width: 400px;
      width: 100%;
    }
    h1 { margin: 0 0 0.5rem; color: #333; }
    p { color: #666; margin-bottom: 2rem; }
    .btn {
      display: block;
      width: 100%;
      padding: 0.875rem 1.5rem;
      border-radius: 4px;
      text-decoration: none;
      font-weight: 500;
      transition: background 0.2s;
      border: none;
      cursor: pointer;
      font-size: 1rem;
    }
    .btn-primary {
      background: #667eea;
      color: white;
    }
    .btn-primary:hover {
      background: #5a6fd6;
    }
    .links {
      margin-top: 1.5rem;
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
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
export class LoginComponent {
  isTempAuth = computed(() => environment.useTempAuth);
  loginUrl = computed(() => `${environment.gatewayUrl}/api/auth/login?returnUrl=${encodeURIComponent(window.location.origin + '/applications')}`);

  constructor(private router: Router, private authService: AuthService) {}

  loginWithTemp(): void {
    this.authService.login();
    this.router.navigate(['/applications']);
  }
}
