import { test, expect } from './fixtures';

test('user signs in as the demo account through the real backend', async ({
  page,
  applicationUrl,
}) => {
  await page.goto(applicationUrl);

  await page.getByLabel('Username').fill('other');
  await page.getByLabel('Password').fill('GroceryDemo1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');

  await page.getByLabel('Username').fill(' DEMO ');
  await page.getByLabel('Password').fill('incorrect');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('alert')).toHaveText('Invalid username or password');

  await page.getByLabel('Password').fill('GroceryDemo1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
});
