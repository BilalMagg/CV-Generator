import { Injectable, inject } from '@angular/core';
import { HttpService } from './http.service';
import { UserProfile, UpdateUserProfileDto } from '../models/user-profile.model';

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: unknown;
}

@Injectable({ providedIn: 'root' })
export class UserProfileService {
  private http = inject(HttpService);

  async getMyProfile(): Promise<UserProfile | null> {
    const res = await this.http.get<ApiResponse<UserProfile>>('/api/users/me');
    return res.data ?? null;
  }

  async updateProfile(id: string, dto: UpdateUserProfileDto): Promise<UserProfile | null> {
    const res = await this.http.put<ApiResponse<UserProfile>>(`/api/users/${id}`, dto as object);
    return res.data ?? null;
  }
}
