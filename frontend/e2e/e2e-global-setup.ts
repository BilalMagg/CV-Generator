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
  const browser = await chromium.launch();
  try {
    const context = await browser.newContext();
    const page = await context.newPage();
    await registerAndSignIn(page);
    await context.storageState({ path: RUNNER_STATE });
    await context.close();
  } finally {
    await browser.close();
  }
}
