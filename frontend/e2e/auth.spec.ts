import { test, expect } from '@playwright/test';
import { signUp, signIn, logout, uniqueUser } from './e2e-utils';

/**
 * Phase 1 — the auth journey, exercised exactly as a real user walks the app:
 * register → get confirmed → back to the sign-in slide → login → the kanban board →
 * session handling (bad password, reload persistence, logout, protected-route guard).
 */

test.describe('auth @smoke', () => {
  test('sign up → confirm → sign in → lands on the applications board', async ({ page }) => {
    const user = uniqueUser();

    await signUp(page, user); // success banner = account created in Keycloak AND backend

    // The form flips back to the sign-in slide after a beat
    await expect(page.locator('form.sign-in-form')).toBeVisible();

    await signIn(page, user.email, user.password);

    // Kanban board is the /applications landing view
    await expect(page.locator('h1.kb-title')).toContainText('Move applications across stages');
    // The freshly created user is reflected in the sidebar identity
    await expect(page.locator('.sidebar .user-email')).toHaveText(user.email);
  });

  test('sign-up validation — weak password and mismatch are rejected', async ({ page }) => {
    const user = uniqueUser();
    await page.goto('/login?mode=sign-up');
    await expect(page.locator('section.sign-up-mode')).toHaveCount(1);

    const form = page.locator('form.sign-up-form');
    await form.locator("input[name='signUpFirstName']").fill(user.firstName);
    await form.locator("input[name='signUpLastName']").fill(user.lastName);
    await form.locator("input[name='signUpEmail']").fill(user.email);
    await form.locator("input[name='signUpPassword']").fill('short'); // < minimum 8
    await form.locator("input[name='signUpConfirm']").fill('short');
    await form.locator("button[type='submit']").click();

    await expect(form.locator('.alert.error')).toContainText('at least 8 characters');
    // Password mismatch path
    await form.locator("input[name='signUpPassword']").fill(user.password);
    await form.locator("input[name='signUpConfirm']").fill('Different!2026');
    await form.locator("button[type='submit']").click();
    await expect(form.locator('.alert.error')).toContainText('do not match');
  });

  test('sign-in with a wrong password shows the error and stays on the form', async ({ page }) => {
    const user = uniqueUser();
    await signUp(page, user);

    await page.goto('/login');
    const form = page.locator('form.sign-in-form');
    await form.locator("input[name='signInEmail']").fill(user.email);
    await form.locator("input[name='signInPassword']").fill('definitely-wrong-pass');
    await form.locator("button[type='submit']").click();

    await expect(form.locator('.alert.error')).toBeVisible();
    // Still on the login page
    await expect(page.locator('form.sign-in-form')).toBeVisible();
  });

  test('session survives reload, then logout drops it and the guard redirects', async ({ page }) => {
    const user = uniqueUser();
    await signUp(page, user);
    await signIn(page, user.email, user.password);
    await expect(page.locator('h1.kb-title')).toContainText('Move applications across stages');

    // Cookies only — no localStorage dependency in the session
    await page.reload();
    await expect(page.locator('h1.kb-title')).toContainText('Move applications across stages');

    await logout(page);
    await expect(page.locator('form.sign-in-form')).toBeVisible();

    // Protected route no longer accessible with the dropped session
    await page.goto('/applications');
    await expect(page.locator('form.sign-in-form')).toBeVisible({ timeout: 15_000 });
  });

  test('a fresh context cannot open protected pages without logging in', async ({ page }) => {
    // No storageState, no prior navigation — straight to a protected route
    await page.goto('/applications');
    await expect(page.locator('form.sign-in-form')).toBeVisible({ timeout: 15_000 });
  });
});
