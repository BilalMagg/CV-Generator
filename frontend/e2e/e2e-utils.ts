import { type Browser, type Page } from '@playwright/test';
import * as path from 'path';

/** Fixed strong test password (>= 8 chars, mixed) used for every E2E account. */
export const E2E_PASSWORD = 'Propel!E2e-2026';

/** Where the global setup persists the authenticated "runner" storage state. */
export const RUNNER_STATE = path.join(__dirname, '.auth', 'runner.json');

/** Unique-per-run test user: each run seeds its own account so re-runs
 * never collide and no pre-seeded stack data is required. */
export function uniqueUser(prefix = 'e2e'): { firstName: string; lastName: string; email: string; password: string } {
  const tag = `${Date.now().toString(36)}${Math.floor(Math.random() * 1e4).toString(36)}`;
  return {
    firstName: 'E2E',
    lastName: `Runner ${tag}`,
    email: `${prefix}-${tag}@propel-tests.dev`,
    password: E2E_PASSWORD,
  };
}

/** True when the sign-up slide is showing (the app toggles opacity, not DOM removal). */
export async function isSignUpMode(page: Page): Promise<boolean> {
  return page.locator('section.sign-up-mode').count() > 0;
}

/** Fill + submit the sign-up form and wait for the success banner. */
export async function signUp(page: Page, user: { firstName: string; lastName: string; email: string; password: string }): Promise<void> {
  await page.goto('/login?mode=sign-up');
  const form = page.locator('form.sign-up-form');
  await form.locator("input[name='signUpFirstName']").fill(user.firstName);
  await form.locator("input[name='signUpLastName']").fill(user.lastName);
  await form.locator("input[name='signUpEmail']").fill(user.email);
  await form.locator("input[name='signUpPassword']").fill(user.password);
  await form.locator("input[name='signUpConfirm']").fill(user.password);
  await form.locator("button[type='submit']").click();
  await page.locator('form.sign-up-form .alert.success').waitFor({ state: 'visible', timeout: 30_000 });
}

/** Fill + submit the sign-in form; resolves once the app lands on /applications. */
export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  const form = page.locator('form.sign-in-form');
  await form.locator("input[name='signInEmail']").fill(email);
  await form.locator("input[name='signInPassword']").fill(password);
  await form.locator("button[type='submit']").click();
  // Full-page redirect to /applications, which the router then replaces with
  // /applications/dashboard — accept both (regex, not glob) so the redirect
  // transit never races the wait.
  await page.waitForURL(/\/applications(\/|$)/, { timeout: 30_000 });
}

/** Sign up AND then sign in from a brand-new browser state — the canonical journey. */
export async function registerAndSignIn(page: Page): Promise<{ email: string; password: string }> {
  const user = uniqueUser();
  await signUp(page, user);
  await signIn(page, user.email, user.password);
  return user;
}

/** Click the sidebar logout icon; resolves once the login form is back in front. */
export async function logout(page: Page): Promise<void> {
  await page.locator('.sidebar .logout-btn').click();
  // Full-page navigation back through Keycloak/gateway to the login screen.
  await page.locator('form.sign-in-form').waitFor({ state: 'visible', timeout: 30_000 });
}

/** Persistent storage state dir — used by later phases to reuse an authenticated session. */
export async function saveSession(browser: Browser, statePath: string): Promise<void> {
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  await registerAndSignIn(page);
  await ctx.storageState({ path: statePath });
  await ctx.close();
}
