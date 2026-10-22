import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';

async function signIn(page: Page, applicationUrl: string) {
  await page.goto(applicationUrl);
  await page.getByLabel('Username').fill('demo');
  await page.getByLabel('Password').fill('GroceryDemo1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
}

test('user creates and selects the first grocery list from the empty state', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);
  await expect(page.getByText('No grocery lists yet.')).toBeVisible();

  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await expect(page.getByRole('button', { name: 'Create', exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Cancel', exact: true })).toBeVisible();
  await page.getByLabel('List name').fill('Discard with button');
  await page.getByRole('button', { name: 'Cancel', exact: true }).click();
  await expect(page.getByLabel('List name')).not.toBeVisible();

  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await page.getByLabel('List name').fill('Discard with keyboard');
  await page.getByLabel('List name').press('Escape');
  await expect(page.getByLabel('List name')).not.toBeVisible();

  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await page.getByLabel('List name').fill('  Weekly\t groceries  ');
  await page.getByLabel('List name').press('Enter');

  const selectedList = page.getByRole('button', { name: 'Weekly groceries' });
  await expect(selectedList).toHaveAttribute('aria-current', 'true');
  await expect(selectedList).toBeInViewport();
  await expect(page.getByText('No grocery lists yet.')).not.toBeVisible();

  await page.reload();

  await expect(page.getByRole('button', { name: 'Weekly groceries' }))
    .toHaveAttribute('aria-current', 'true');
});

test('session loss discards a list draft and does not replay its create', async ({
  browser,
  applicationUrl,
}) => {
  const editingContext = await browser.newContext();
  const revokingContext = await browser.newContext();
  const editingPage = await editingContext.newPage();
  const revokingPage = await revokingContext.newPage();
  await signIn(editingPage, applicationUrl);
  await signIn(revokingPage, applicationUrl);
  await editingPage.getByRole('button', { name: 'Create a grocery list' }).click();
  await editingPage.getByLabel('List name').fill('Should not exist');

  await revokingPage.getByRole('button', { name: 'Log out everywhere' }).click();
  await editingPage.getByLabel('List name').press('Enter');

  await expect(editingPage.getByRole('heading', { name: 'Sign in' })).toBeVisible();
  await expect(editingPage.getByRole('alert')).toHaveText(
    'Your session ended. Unsaved changes were discarded.',
  );
  await editingPage.getByLabel('Username').fill('demo');
  await editingPage.getByLabel('Password').fill('GroceryDemo1!');
  await editingPage.getByRole('button', { name: 'Sign in' }).click();
  await expect(editingPage.getByText('No grocery lists yet.')).toBeVisible();
  await expect(editingPage.getByLabel('List name')).not.toBeVisible();
  await expect(editingPage.getByText('Should not exist')).not.toBeVisible();

  await editingContext.close();
  await revokingContext.close();
});
