import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { firstValueFrom } from 'rxjs';

const OPTS = { withCredentials: true };

@Injectable({
  providedIn: 'root'
})
export class HttpService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiUrl;

  async get<T>(path: string): Promise<T> {
    return firstValueFrom(this.http.get<T>(`${this.baseUrl}${path}`, OPTS));
  }

  async post<T>(path: string, body: object): Promise<T> {
    return firstValueFrom(this.http.post<T>(`${this.baseUrl}${path}`, body, OPTS));
  }

  async put<T>(path: string, body: object): Promise<T> {
    return firstValueFrom(this.http.put<T>(`${this.baseUrl}${path}`, body, OPTS));
  }

  async patch<T>(path: string, body: object): Promise<T> {
    return firstValueFrom(this.http.patch<T>(`${this.baseUrl}${path}`, body, OPTS));
  }

  async delete<T>(path: string): Promise<T> {
    return firstValueFrom(this.http.delete<T>(`${this.baseUrl}${path}`, OPTS));
  }
}
