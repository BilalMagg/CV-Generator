import { Injectable, signal, computed } from '@angular/core';
import { HttpService } from './http.service';
import { environment } from '../../environments/environment';

export interface User {
  userId: string;
  keycloakId: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  isActive: boolean;
  tokens?: {
    accessToken?: string;
    refreshToken?: string;
    idToken?: string;
    expiresAt?: string;
    hasRefreshToken: boolean;
  };
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: unknown;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'access_token';

  currentUser = signal<User | null>(null);
  isAuthenticated = computed(() => this.currentUser() !== null);

  constructor(private http: HttpService) {}

  async checkAuth(): Promise<boolean> {
    try {
      const response = await this.http.get<ApiResponse<User>>('/api/auth/me');
      if (response.success && response.data) {
        this.currentUser.set(response.data);
        return true;
      }
    } catch {
      // Not authenticated or session expired
    }
    this.currentUser.set(null);
    return false;
  }

  async refreshUser(): Promise<void> {
    try {
      const response = await this.http.get<ApiResponse<User>>('/api/auth/me');
      if (response.success && response.data) {
        this.currentUser.set(response.data);
      }
    } catch {
      this.currentUser.set(null);
    }
  }

  login(): void {
    // Redirect to backend OIDC login endpoint
    window.location.href = `${environment.apiUrl}/api/auth/login`;
  }

  logout(): void {
    // Clear local state
    this.currentUser.set(null);
    localStorage.removeItem(this.TOKEN_KEY);
    // Redirect to backend logout endpoint
    window.location.href = `${environment.apiUrl}/api/auth/logout`;
  }

  async register(data: {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
  }): Promise<{ success: boolean; message: string }> {
    try {
      const response = await this.http.post<ApiResponse<object>>('/api/auth/register', data);
      if (response.success) {
        return { success: true, message: response.message || 'Registration successful' };
      }
      return { success: false, message: response.message || 'Registration failed' };
    } catch (err) {
      return { success: false, message: err instanceof Error ? err.message : 'Registration failed' };
    }
  }

  getAccessToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  setAccessToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }
}
