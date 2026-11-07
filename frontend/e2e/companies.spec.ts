import { test, expect } from '@playwright/test';
import { registerAndSignIn } from './e2e-utils';

/**
 * Phase 2a — Companies: create → search → edit → delete through the real UI,
 * each against a fresh per-run account (suite is self-contained; no pre-seeded
 * data required). Name uniqueness is guaranteed per run via a timestamp tag.
 */
function uniqueName(product: string): string {
  return `E2E Co ${product} ${Date.now().toString(36)}${Math.floor(Math.random() * 1e3).toString(36)}`;
}

test.describe('companies', () => {
  test.beforeEach(async ({ page }) => {
    await registerAndSignIn(page);
    await page.goto('/companies');
    await expect(page.locator('h1', { hasText: 'Companies' })).toBeVisible();
    // A fresh account always starts with no companies
    await expect(page.getByText('No companies yet — add the ones')).toBeVisible();
  });

  test('create a company → row appears in the list → detail page shows it', async ({ page }) => {
    const name = uniqueName(['Alpha']);

    await page.getByRole('button', { name: 'Add company' }).click();
    const dialog = page.locator('.modal', { has: page.locator('h2', { hasText: 'Add company' }) });
    await expect(dialog).toBeVisible();
    await dialog.locator("input[name='coName']").fill(name);
    await dialog.locator("input[name='coWebsite']").fill('https://example-corp.com');
    await dialog.locator("input[name='coCity']").fill('Casablanca');
    await dialog.locator("textarea[name='coNote']").fill('Created by the companies E2E suite');
    await dialog.getByRole('button', { name: 'Add company' }).click();

    // Dialog closes, toast confirms, row renders
    await expect(dialog).toBeHidden();
    await expect(page.locator('.toast-container .toast', { hasText: 'Company added' })).toBeVisible();
    const row = page.locator('tr.row-link', { hasText: name });
    await expect(row).toBeVisible();
    await expect(row.locator('.co-host')).toHaveText('example-corp.com');

    // Row click opens the detail page
    await row.click();
    await expect(page).toHaveURL(/companies\/[0-9a-f-]{36}$/);
    await expect(page.locator('h1', { hasText: name })).toBeVisible();
    await expect(page.locator('.co-note', { hasText: 'Created by the companies E2E suite' })).toBeVisible();
  });

  test('create dialog validation — empty name is rejected with a toast', async ({ page }) => {
    await page.getByRole('button', { name: 'Add company' }).click();
    const dialog = page.locator('.modal');
    await dialog.getByRole('button', { name: 'Add company' }).click();
    await expect(page.locator('.toast-container .toast', { hasText: 'Company name is required' })).toBeVisible();
    // Nothing was saved and the dialog stays open for correction
    await expect(dialog).toBeVisible();
    await expect(page.locator('tr.row-link')).toHaveCount(0);
  });

  test('search filters the table; clearing restores it', async ({ page }) => {
    const name = uniqueName(['Gamma']);
    await page.getByRole('button', { name: 'Add company' }).click();
    const dialog = page.locator('.modal');
    await dialog.locator("input[name='coName']").fill(name);
    await dialog.getByRole('button', { name: 'Add company' }).click();
    await expect(page.locator('tr.row-link', { hasText: name })).toBeVisible();

    const searchBox = page.locator('.search-box input');
    await searchBox.fill(name);
    // Debounced (300ms) server-side search: row matches, no empty state
    await expect(page.locator('tr.row-link', { hasText: name })).toBeVisible();
    await expect(page.locator('tr.row-link')).toHaveCount(1);

    await searchBox.fill('zzz-no-such-company');
    await expect(page.getByText('No companies match your filters.')).toBeVisible();
    await expect(page.locator('tr.row-link')).toHaveCount(0);

    // Reset restores the row (Reset button appears with active filters)
    await page.getByRole('button', { name: 'Reset' }).click();
    await expect(page.locator('tr.row-link', { hasText: name })).toBeVisible();
  });

  test('edit a company from its row', async ({ page }) => {
    const name = uniqueName(['Delta']);
    await page.getByRole('button', { name: 'Add company' }).click();
    const dialog = page.locator('.modal');
    await dialog.locator("input[name='coName']").fill(name);
    await dialog.getByRole('button', { name: 'Add company' }).click();
    const row = page.locator('tr.row-link', { hasText: name });
    await expect(row).toBeVisible();

    await row.getByTitle('Edit').click();
    const editDialog = page.locator('.modal', { has: page.locator('h2', { hasText: 'Edit company' }) });
    await expect(editDialog).toBeVisible();
    // Prefilled with the current record
    await expect(editDialog.locator("input[name='coName']")).toHaveValue(name);

    const renamed = `${name} Ltd`;
    await editDialog.locator("input[name='coName']").fill(renamed);
    await editDialog.getByRole('button', { name: 'Save changes' }).click();
    await expect(editDialog).toBeHidden();
    await expect(page.locator('.toast-container .toast', { hasText: 'Company updated' })).toBeVisible();
    // Old row is gone, renamed row is in its place
    await expect(page.locator('tr.row-link')).toHaveCount(1);
    await expect(page.locator('tr.row-link').first()).toContainText(renamed);
  });

  test('delete asks for confirmation; cancel keeps the row, confirm removes it', async ({ page }) => {
    const name = uniqueName(['Sigma']);
    await page.getByRole('button', { name: 'Add company' }).click();
    const dialog = page.locator('.modal');
    await dialog.locator("input[name='coName']").fill(name);
    await dialog.getByRole('button', { name: 'Add company' }).click();
    const row = page.locator('tr.row-link', { hasText: name });
    await expect(row).toBeVisible();

    // Cancel path
    await row.getByTitle('Delete').click();
    const confirmDialog = page.locator('.confirm-dialog');
    await expect(confirmDialog).toContainText(`Remove "${name}" from your list?`);
    await confirmDialog.getByRole('button', { name: 'Cancel' }).click();
    await expect(confirmDialog).toBeHidden();
    await expect(row).toBeVisible();

    // Confirm path
    await row.getByTitle('Delete').click();
    await confirmDialog.getByRole('button', { name: 'Confirm' }).click();
    await expect(confirmDialog).toBeHidden();
    await expect(row).toBeHidden();
    await expect(page.locator('tr.row-link')).toHaveCount(0);
    await expect(page.getByText('No companies yet — add the ones')).toBeVisible();
  });
});
