import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';

async function signIn(page: Page, applicationUrl: string) {
  await page.goto(applicationUrl);
  await page.getByLabel('Username').fill('demo');
  await page.getByLabel('Password').fill('GroceryDemo1!');
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page.getByRole('heading', { name: 'Signed in as demo' })).toBeVisible();
}

async function createList(page: Page, name: string) {
  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await page.getByLabel('List name').fill(name);
  await page.getByRole('button', { name: 'Create', exact: true }).click();
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

test('user creates, selects, and moves between multiple grocery lists', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);
  await page.setViewportSize({ width: 1280, height: 320 });

  await createList(page, 'Weekly groceries');
  await createList(page, 'Market');

  const weeklyList = page.getByRole('button', { name: 'Weekly groceries' });
  const marketList = page.getByRole('button', { name: 'Market' });
  await expect(marketList).toHaveAttribute('aria-current', 'true');
  await expect(marketList).toBeInViewport();
  await weeklyList.click();
  await expect(weeklyList).toHaveAttribute('aria-current', 'true');
  await expect(marketList).not.toHaveAttribute('aria-current', 'true');

  for (let index = 1; index <= 15; index += 1) {
    await createList(page, `List ${index}`);
  }
  await createList(page, 'A list');
  const alphabeticallyFirstList = page.getByRole('button', { name: 'A list' });
  await expect(alphabeticallyFirstList).toHaveAttribute('aria-current', 'true');
  await expect(alphabeticallyFirstList).toBeInViewport();
});

test('user sorts lists naturally in either direction and the direction survives refresh', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);
  for (const name of ['Shop 10', 'Market', 'shop 2']) {
    await createList(page, name);
  }

  const listButtons = page.getByRole('list').getByRole('button');
  await expect(page.getByLabel('List order')).toHaveValue('ascending');
  await expect(listButtons).toHaveText(['Market', 'shop 2', 'Shop 10']);
  await expect(page.getByRole('button', { name: 'shop 2' }))
    .toHaveAttribute('aria-current', 'true');

  await page.getByLabel('List order').selectOption('descending');
  await expect(listButtons).toHaveText(['Shop 10', 'shop 2', 'Market']);

  await page.reload();

  await expect(page.getByLabel('List order')).toHaveValue('descending');
  await expect(listButtons).toHaveText(['Shop 10', 'shop 2', 'Market']);
  await expect(page.getByRole('button', { name: 'Shop 10' }))
    .toHaveAttribute('aria-current', 'true');
});

test('invalid saved list order safely falls back to A–Z', async ({
  page,
  applicationUrl,
}) => {
  await page.addInitScript(() => {
    localStorage.setItem('grocery-helper:list-order', 'sideways');
  });
  await signIn(page, applicationUrl);
  await createList(page, 'Market');

  await expect(page.getByLabel('List order')).toHaveValue('ascending');
});

test('browser sessions keep independent list order and selection', async ({
  browser,
  applicationUrl,
}) => {
  const firstContext = await browser.newContext();
  const secondContext = await browser.newContext();
  const firstPage = await firstContext.newPage();
  const secondPage = await secondContext.newPage();
  await signIn(firstPage, applicationUrl);
  for (const name of ['Market', 'Shop 2', 'Shop 10']) {
    await createList(firstPage, name);
  }
  await firstPage.getByRole('button', { name: 'Market' }).click();

  await signIn(secondPage, applicationUrl);
  await secondPage.getByLabel('List order').selectOption('descending');
  await secondPage.getByRole('button', { name: 'Shop 2' }).click();

  await expect(firstPage.getByLabel('List order')).toHaveValue('ascending');
  await expect(firstPage.getByRole('list').getByRole('button'))
    .toHaveText(['Market', 'Shop 2', 'Shop 10']);
  await expect(firstPage.getByRole('button', { name: 'Market' }))
    .toHaveAttribute('aria-current', 'true');
  await expect(secondPage.getByLabel('List order')).toHaveValue('descending');
  await expect(secondPage.getByRole('list').getByRole('button'))
    .toHaveText(['Shop 10', 'Shop 2', 'Market']);
  await expect(secondPage.getByRole('button', { name: 'Shop 2' }))
    .toHaveAttribute('aria-current', 'true');

  await firstContext.close();
  await secondContext.close();
});

test('duplicate creation can be revised after OK or Escape', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);
  await createList(page, 'Weekly groceries');
  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await page.getByLabel('List name').fill('weekly GROCERIES');
  const createButton = page.getByRole('button', { name: 'Create', exact: true });

  await createButton.click();

  const dialog = page.getByRole('dialog', { name: 'Grocery list already exists' });
  await expect(dialog).toContainText('Weekly groceries');
  await expect(dialog.getByRole('button', { name: 'OK', exact: true })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Navigate', exact: true })).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'OK', exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'Navigate', exact: true })).toBeFocused();
  await page.keyboard.press('Tab');
  await expect(dialog.getByRole('button', { name: 'OK', exact: true })).toBeFocused();
  await dialog.getByRole('button', { name: 'OK', exact: true }).click();
  await expect(dialog).not.toBeVisible();
  await expect(page.getByLabel('List name')).toHaveValue('weekly GROCERIES');
  await expect(createButton).toBeFocused();

  await createButton.click();
  await expect(dialog).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(page.getByLabel('List name')).toHaveValue('weekly GROCERIES');
  await expect(createButton).toBeFocused();
});

test('duplicate creation can navigate to the existing list in the current order', async ({
  page,
  applicationUrl,
}) => {
  await signIn(page, applicationUrl);
  await createList(page, 'Market');
  await createList(page, 'Weekly groceries');
  await page.getByLabel('List order').selectOption('descending');
  await page.getByRole('button', { name: 'Create a grocery list' }).click();
  await page.getByLabel('List name').fill('MARKET');
  await page.getByRole('button', { name: 'Create', exact: true }).click();

  await page.getByRole('dialog', { name: 'Grocery list already exists' })
    .getByRole('button', { name: 'Navigate', exact: true })
    .click();

  const market = page.getByRole('list').getByRole('button', { name: 'Market' });
  await expect(page.getByRole('dialog')).not.toBeVisible();
  await expect(page.getByLabel('List name')).not.toBeVisible();
  await expect(market).toHaveAttribute('aria-current', 'true');
  await expect(market).toBeInViewport();
  await expect(market).toBeFocused();
  await expect(page.getByRole('list').getByRole('button')).toHaveText([
    'Weekly groceries',
    'Market',
  ]);
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
