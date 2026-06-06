import { HttpErrorResponse } from '@angular/common/http';

export function extractError(err: unknown, fallback = 'Something went wrong'): string {
  if (err instanceof HttpErrorResponse) {
    if (err.error?.message) return err.error.message;
    if (err.status === 0) return 'Network error — check your connection';
    if (err.status >= 500) return 'Server error — try again later';
    return err.message || fallback;
  }
  return err instanceof Error ? err.message : fallback;
}
