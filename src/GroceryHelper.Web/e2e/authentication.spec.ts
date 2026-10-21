import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';

async function signIn(page: Page, applicationUrl: string) {
  await page.goto(applicationUrl);
  await page.getByLabel('Username').fill('demo');
  await page.getByLabel('Password').fill('GroceryDemo1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
}

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

test('refresh restores the authenticated session', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);

  await page.reload();

  await expect(page.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
});

test('reopening a browser restores its unexpired session', async ({
  browser,
  applicationUrl,
}) => {
  const originalBrowser = await browser.newContext();
  const originalPage = await originalBrowser.newPage();
  await signIn(originalPage, applicationUrl);
  const persistedState = await originalBrowser.storageState();
  await originalBrowser.close();

  const reopenedBrowser = await browser.newContext({ storageState: persistedState });
  const reopenedPage = await reopenedBrowser.newPage();
  await reopenedPage.goto(applicationUrl);

  await expect(reopenedPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  await reopenedBrowser.close();
});

test('two browsers remain signed in simultaneously', async ({
  browser,
  applicationUrl,
}) => {
  const firstBrowser = await browser.newContext();
  const secondBrowser = await browser.newContext();
  const firstPage = await firstBrowser.newPage();
  const secondPage = await secondBrowser.newPage();
  await signIn(firstPage, applicationUrl);

  await signIn(secondPage, applicationUrl);
  await firstPage.reload();

  await expect(firstPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  await expect(secondPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  await firstBrowser.close();
  await secondBrowser.close();
});
