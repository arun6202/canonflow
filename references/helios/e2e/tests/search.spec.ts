import { test, expect } from '@playwright/test';

test.describe('Search Platform E2E Tests', () => {
  
  test('Visual Builder executes correctly and returns data', async ({ page }) => {
    // 1. Navigate to the frontend
    await page.goto('/');

    // 2. We should be on the Visual Builder tab by default
    await expect(page.locator('h1')).toContainText('SOTA Search Platform');
    
    // The "Execute Visual Query" button should be visible
    const executeBtn = page.getByRole('button', { name: 'Execute Visual Query' });
    await expect(executeBtn).toBeVisible();

    // 3. Fill in the value and execute
    await page.locator('input[type="text"]').first().fill('USA');
    await executeBtn.click();

    // 4. Verify we don't hit the 400 Bad Request error. We should see "Found X Hits"
    // The backend should properly parse the Thoth serialization and return ES results.
    const hitsText = page.locator('h3', { hasText: 'Found' });
    await expect(hitsText).toBeVisible({ timeout: 10000 });
    
    // Check if table rows are loaded
    const rows = page.locator('tbody tr');
    await expect(async () => {
      expect(await rows.count()).toBeGreaterThan(0);
    }).toPass();
  });

  test('Text Search parser parses query and executes successfully', async ({ page }) => {
    await page.goto('/');
    
    // Switch to Text Search Tab
    await page.locator('div').filter({ hasText: /^Text Search$/ }).click();

    // Verify parser UI
    const parserInput = page.locator('input[type="text"]');
    await expect(parserInput).toBeVisible();

    // Fill with a supported query
    await parserInput.fill('Country:USA AND ProductCategory:Condiments');

    // Parse and Execute
    const parseBtn = page.getByRole('button', { name: 'Parse & Execute' });
    await parseBtn.click();

    // Wait for the results
    const hitsText = page.locator('h3', { hasText: 'Found' });
    await expect(hitsText).toBeVisible({ timeout: 10000 });
  });

  test('Analytics Builder returns data without serialization errors', async ({ page }) => {
    await page.goto('/');

    // Switch to Analytics Tab
    await page.locator('div').filter({ hasText: /^Analytics$/ }).click();

    // Click "Run Analytics"
    const runBtn = page.getByRole('button', { name: 'Run Analytics' });
    await runBtn.click();

    // Verify we get BI Results (which means Thoth parsing worked on backend)
    const biResults = page.locator('h3', { hasText: 'Business Intelligence Results' });
    await expect(biResults).toBeVisible({ timeout: 10000 });

    // Verify buckets are rendered
    const bucketTable = page.locator('table');
    await expect(bucketTable).toBeVisible();
    await expect(async () => {
      expect(await page.locator('tbody tr').count()).toBeGreaterThan(0);
    }).toPass();
  });
});
