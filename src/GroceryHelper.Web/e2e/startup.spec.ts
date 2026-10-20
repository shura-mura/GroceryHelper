import { test, expect } from './fixtures';

test('scaffold connects the browser to the real backend', async ({ page, applicationUrl }) => {
  await page.goto(applicationUrl);

  await expect(page.getByRole('heading', { name: 'GroceryHelper', exact: true })).toBeVisible();
  await expect(page.getByRole('status')).toHaveText('Backend connected');
});
