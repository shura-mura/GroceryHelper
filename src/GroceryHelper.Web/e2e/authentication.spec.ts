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
}, testInfo) => {
  const profilePath = testInfo.outputPath('browser-profile');
  const originalContext = await browser.browserType().launchPersistentContext(profilePath);
  try {
    const originalPage = await originalContext.newPage();
    await signIn(originalPage, applicationUrl);
  } finally {
    await originalContext.close();
  }

  const reopenedContext = await browser.browserType().launchPersistentContext(profilePath);
  try {
    const reopenedPage = await reopenedContext.newPage();
    await reopenedPage.goto(applicationUrl);

    await expect(reopenedPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  } finally {
    await reopenedContext.close();
  }
});

test('two browsers remain signed in simultaneously', async ({
  browser,
  applicationUrl,
}) => {
  const firstContext = await browser.newContext();
  const secondContext = await browser.newContext();
  const firstPage = await firstContext.newPage();
  const secondPage = await secondContext.newPage();
  await signIn(firstPage, applicationUrl);

  await signIn(secondPage, applicationUrl);
  await firstPage.reload();

  await expect(firstPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  await expect(secondPage.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
  await firstContext.close();
  await secondContext.close();
});
