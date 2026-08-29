import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, timeout, TimeoutError, throwError } from 'rxjs';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { ToastService } from './toast.service';

const REQUEST_TIMEOUT = 30_000;

const AUTH_ENDPOINTS = [
  '/api/auth/login',
  '/api/auth/register',
  '/api/auth/logout',
  '/api/auth/me',
];

export const httpInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const toast = inject(ToastService);

  return next(req).pipe(
    timeout(REQUEST_TIMEOUT),
    catchError((err: unknown) => {
      if (err instanceof TimeoutError) {
        toast.error('Request timed out — please try again');
        return throwError(() => new Error('Request timed out — please try again'));
      }

      if (err instanceof HttpErrorResponse) {
        if (err.status === 401 && !AUTH_ENDPOINTS.some(e => req.url.includes(e))) {
          toast.error('Session expired — please sign in again');
          router.navigate(['/login']);
          return throwError(() => new Error('Session expired'));
        }

        const message =
          err.error?.message ||
          (err.status === 0 ? 'Network error — check your connection' :
           err.status >= 500 ? 'Server error — try again later' :
           err.message || 'Request failed');

        if (err.status === 0 || err.status >= 500) {
          toast.error(message);
        }

        return throwError(() => new Error(message));
      }

      return throwError(() => err);
    }),
  );
};
