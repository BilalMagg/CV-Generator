import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { AuthService } from '../services/auth.service';

const AUTH_TIMEOUT = 10_000;

export const authGuard: CanActivateFn = async (route, state) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const isAuthenticated = await Promise.race([
    authService.checkAuth(),
    new Promise<boolean>(resolve => setTimeout(() => resolve(false), AUTH_TIMEOUT)),
  ]);

  if (!isAuthenticated) {
    router.navigate(['/login'], { queryParams: { returnUrl: state.url } });
    return false;
  }

  return true;
};
