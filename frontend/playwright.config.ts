import { defineConfig, devices } from '@playwright/test';

/**
 * E2E tests for the PROPUL stack booted from `deploy/docker-compose.yml`.
 * Base URL is the SPA served by the container's nginx (:4200 default) — /api flows
 * through the same origin to the gateway, exactly like a real browser session.
 *
 * Run: `npm run e2e`            (headless)
 *      `npm run e2e:ui`         (Playwright UI mode)
 *      `--grep @smoke`          (fast triage set)
 */
export default defineConfig({
  testDir: './e2e',
  testMatch: '**/*.spec.ts',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false, // intentional: journeys share stack state conventions
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI
    ? [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]]
    : [['list'], ['html', { open: 'on-failure', outputFolder: 'playwright-report' }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ...devices['Desktop Chrome'],
    viewport: { width: 1440, height: 900 },
  },
  outputDir: 'test-results',
  globalSetup: './e2e/e2e-global-setup.ts',
});
