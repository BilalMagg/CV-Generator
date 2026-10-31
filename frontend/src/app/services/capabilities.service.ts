import { Injectable, inject, signal } from '@angular/core';
import { HttpService } from './http.service';

interface Envelope<T> {
  success: boolean;
  data?: T;
}

export interface SystemCapabilities {
  latexEngineAvailable: boolean;
  /** "native" | "docker-texlive" | null */
  latexEngine: string | null;
  latexEngineReason: string | null;
  textPdfAvailable: boolean;
  sidecarReachable: boolean;
}

/**
 * Runtime deployment capabilities per user (GET /api/system/capabilities).
 *
 * The app must degrade gracefully when the deployment has no LaTeX engine:
 * CV .tex sources still generate, PDF compilation is disabled with a
 * compile-on-Overleaf hint. Loaded lazily on the first consumer, cached for
 * 60s. Consumers must treat `latexAvailable() === null` as "unknown yet"
 * and not block anything on it.
 */
@Injectable({ providedIn: 'root' })
export class CapabilitiesService {
  private readonly http = inject(HttpService);

  /** null = not probed yet / unknown — never used to disable controls. */
  readonly latexAvailable = signal<boolean | null>(null);
  readonly engineMode = signal<string | null>(null);
  readonly engineReason = signal<string | null>(null);
  readonly textPdfAvailable = signal<boolean | null>(null);

  private static readonly TTL_MS = 60_000;
  private inflight: Promise<void> | null = null;
  private lastFetchedAt = 0;

  /** Fetches capabilities once per page session (cached TTL, deduped). */
  async load(force = false): Promise<void> {
    const fresh = Date.now() - this.lastFetchedAt < CapabilitiesService.TTL_MS;
    if (this.inflight && (fresh || !force)) return this.inflight;
    if (fresh && !force) return;

    this.inflight = (async () => {
      try {
        const res = await this.http.get<Envelope<SystemCapabilities>>('/api/system/capabilities');
        const caps = res.data;
        if (caps) {
          this.latexAvailable.set(caps.latexEngineAvailable);
          this.engineMode.set(caps.latexEngine ?? null);
          this.engineReason.set(caps.latexEngineReason ?? null);
          this.textPdfAvailable.set(caps.textPdfAvailable);
          this.lastFetchedAt = Date.now();
        }
      } catch {
        // Unknown capabilities degrade to "probe failed" — banner hidden,
        // controls stay enabled, backend errors speak for themselves.
        this.lastFetchedAt = Date.now();
      } finally {
        this.inflight = null;
      }
    })();
    return this.inflight;
  }
}
