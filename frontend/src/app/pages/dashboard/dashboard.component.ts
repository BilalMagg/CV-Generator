import { Component, OnInit, signal } from '@angular/core';
import { AuthService, User } from '../../services/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `
    <div class="container">
      <header class="header">
        <h1>Dashboard</h1>
        <button (click)="logout()" class="btn-logout">Logout</button>
      </header>

      <main class="content">
        @if (loading()) {
          <p>Loading...</p>
        } @else if (user()) {
          <div class="user-info">
            <h2>Welcome, {{ user()?.firstName }} {{ user()?.lastName }}!</h2>

            <div class="card">
              <h3>Profile Information</h3>
              <dl>
                <dt>Email</dt>
                <dd>{{ user()?.email }}</dd>

                <dt>Role</dt>
                <dd>{{ user()?.role }}</dd>

                <dt>Status</dt>
                <dd>{{ user()?.isActive ? 'Active' : 'Inactive' }}</dd>

                <dt>User ID</dt>
                <dd>{{ user()?.userId }}</dd>

                <dt>Keycloak ID</dt>
                <dd>{{ user()?.keycloakId }}</dd>
              </dl>
            </div>

            @if (user()?.tokens?.hasRefreshToken) {
              <div class="card">
                <h3>Session</h3>
                <p>You have an active session with refresh token available.</p>
                <p class="token-info">Token expires: {{ user()?.tokens?.expiresAt || 'Unknown' }}</p>
              </div>
            }
          </div>
        } @else {
          <p>Unable to load user information.</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .container {
      min-height: 100vh;
      background: #f5f5f5;
    }
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 2rem;
      background: white;
      box-shadow: 0 1px 2px rgba(0,0,0,0.1);
    }
    .header h1 { margin: 0; }
    .btn-logout {
      padding: 0.5rem 1rem;
      background: #dc3545;
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
    }
    .btn-logout:hover { background: #c82333; }
    .content {
      padding: 2rem;
      max-width: 800px;
      margin: 0 auto;
    }
    .user-info h2 {
      margin-bottom: 1.5rem;
    }
    .card {
      background: white;
      padding: 1.5rem;
      border-radius: 8px;
      box-shadow: 0 1px 3px rgba(0,0,0,0.1);
      margin-bottom: 1rem;
    }
    .card h3 {
      margin-top: 0;
      margin-bottom: 1rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #eee;
    }
    dl {
      display: grid;
      grid-template-columns: 120px 1fr;
      gap: 0.5rem;
      margin: 0;
    }
    dt {
      font-weight: 500;
      color: #666;
    }
    dd {
      margin: 0;
      word-break: break-all;
    }
    .token-info {
      font-size: 0.9rem;
      color: #666;
      margin-top: 0.5rem;
    }
  `]
})
export class DashboardComponent implements OnInit {
  loading = signal(true);
  user = signal<User | null>(null);

  constructor(private authService: AuthService) {}

  ngOnInit(): void {
    this.loadUser();
  }

  async loadUser(): Promise<void> {
    this.loading.set(true);
    await this.authService.refreshUser();
    this.user.set(this.authService.currentUser());
    this.loading.set(false);
  }

  logout(): void {
    this.authService.logout();
  }
}
