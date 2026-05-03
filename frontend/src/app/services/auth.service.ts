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

const TEMP_USER: User = {
  userId: environment.tempUserId,
  keycloakId: 'temp-keycloak-id',
  firstName: 'Temp',
  lastName: 'User',
  email: 'temp@example.com',
  role: 'user',
  isActive: true,
};

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly TOKEN_KEY = 'access_token';

  currentUser = signal<User | null>(null);
  isAuthenticated = computed(() => this.currentUser() !== null);

  constructor(private http: HttpService) {
    if (environment.useTempAuth) {
      this.currentUser.set(TEMP_USER);
    }
  }

  async checkAuth(): Promise<boolean> {
    if (environment.useTempAuth) {
      return true;
    }
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
    if (environment.useTempAuth) return;
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
    if (environment.useTempAuth) {
      this.currentUser.set(TEMP_USER);
      return;
    }
    // Redirect to backend OIDC login endpoint
    window.location.href = `${environment.gatewayUrl}/api/auth/login`;
  }

  logout(): void {
    if (environment.useTempAuth) {
      this.currentUser.set(null);
      return;
    }
    // Clear local state
    this.currentUser.set(null);
    localStorage.removeItem(this.TOKEN_KEY);
    // Redirect to backend logout endpoint
    window.location.href = `${environment.gatewayUrl}/api/auth/logout`;
  }

  async register(data: {
    firstName: string;
    lastName: string;
    email: string;
    password: string;
  }): Promise<{ success: boolean; message: string }> {
    if (environment.useTempAuth) {
      return { success: true, message: 'Registration disabled in temp auth mode' };
    }
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
    if (environment.useTempAuth) return null;
    return localStorage.getItem(this.TOKEN_KEY);
  }

  setAccessToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }
}
