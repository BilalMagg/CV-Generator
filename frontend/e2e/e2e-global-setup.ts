import type { FullConfig } from '@playwright/test';

/**
 * Prepares the authenticated "runner" session used by functionally-focused suites
 * (companies/applications/career/…) so they don't repeat the signup journey.
 * Phase 1 (auth.spec.ts) walks signup itself and does NOT use this state.
 *
 * State is stored at `e2e/.auth/runner.json` (gitignored).
 */
export default async function globalSetup(_config: FullConfig): Promise<void> {
  const { chromium } = await import('@playwright/test');
  const { registerAndSignIn, RUNNER_STATE } = await import('./e2e-utils');
  // Manually launched contexts do NOT inherit the config's `use.baseURL`
  // (that only applies to test-fixture contexts) — resolve it the same way
  // playwright.config.ts does, or page.goto('/login…') is an invalid URL.
  const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:4200';
  const browser = await chromium.launch();
  try {
    const context = await browser.newContext({ baseURL });
    const page = await context.newPage();
    await registerAndSignIn(page);
    await context.storageState({ path: RUNNER_STATE });
    await context.close();
  } finally {
    await browser.close();
  }
}
