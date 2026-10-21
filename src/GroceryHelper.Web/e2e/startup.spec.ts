import { test, expect } from './fixtures';
import type { Page } from '@playwright/test';

interface BrowserDiagnostic {
  service: {
    name: string;
    version: string;
    environment: string;
  };
  span: {
    name: string;
    traceId: string;
  };
}

const diagnosticPrefix = 'GroceryHelper browser telemetry ';

function collectBrowserDiagnostics(page: Page): BrowserDiagnostic[] {
  const diagnostics: BrowserDiagnostic[] = [];
  page.on('console', (message) => {
    if (message.text().startsWith(diagnosticPrefix)) {
      diagnostics.push(JSON.parse(message.text().slice(diagnosticPrefix.length)) as BrowserDiagnostic);
    }
  });
  return diagnostics;
}

test('scaffold connects the browser to the real backend', async ({ page, applicationUrl }) => {
  const healthRequest = page.waitForRequest((request) => new URL(request.url()).pathname === '/health');

  await page.goto(applicationUrl);

  await expect(page.getByRole('heading', { name: 'GroceryHelper', exact: true })).toBeVisible();
  await expect(page.getByRole('status')).toHaveText('Backend connected');

  const traceparent = (await healthRequest).headers()['traceparent'];
  expect(traceparent).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$/);
});

test('browser diagnostics identify page-load and same-origin fetch spans', async ({ page, applicationUrl }) => {
  const diagnostics = collectBrowserDiagnostics(page);

  await page.goto(applicationUrl);
  await expect(page.getByRole('status')).toHaveText('Backend connected');

  await expect.poll(() => diagnostics.map((diagnostic) => diagnostic.span.name)).toEqual(
    expect.arrayContaining(['documentLoad', 'GET /health']),
  );
  expect(diagnostics[0].service).toEqual({
    name: 'GroceryHelper.Web',
    version: '0.0.0',
    environment: 'testing',
  });
});

test('trace context is limited to same-origin API and development health requests', async ({ page, applicationUrl }) => {
  const diagnostics = collectBrowserDiagnostics(page);
  await page.route('https://outside.example/**', async (route) => {
    await route.fulfill({ status: 204 });
  });
  await page.goto(applicationUrl);
  await expect(page.getByRole('status')).toHaveText('Backend connected');

  const apiRequest = page.waitForRequest((request) => request.url().includes('/api/missing'));
  const apiResponse = page.waitForResponse((response) => response.url().includes('/api/missing'));
  const sameOriginPageRequest = page.waitForRequest((request) => request.url().includes('/plain-page'));
  const crossOriginRequest = page.waitForRequest((request) => request.url().startsWith('https://outside.example/'));
  await page.evaluate(async () => {
    await Promise.all([
      fetch('/api/missing?grocery=private-value'),
      fetch('/plain-page'),
      fetch('https://outside.example/api?credential=private-value'),
    ]);
  });

  const apiTraceparent = (await apiRequest).headers()['traceparent'];
  expect(apiTraceparent).toMatch(/^00-[0-9a-f]{32}-[0-9a-f]{16}-0[01]$/);
  await expect((await apiResponse).json()).resolves.toMatchObject({
    traceId: apiTraceparent?.split('-')[1],
  });
  await expect.poll(
    () => diagnostics.find((diagnostic) => diagnostic.span.name === 'GET /api')?.span.traceId,
  ).toBe(apiTraceparent?.split('-')[1]);
  expect((await apiRequest).headers()['baggage']).toBeUndefined();
  expect((await sameOriginPageRequest).headers()['traceparent']).toBeUndefined();
  expect((await crossOriginRequest).headers()['traceparent']).toBeUndefined();
  expect(JSON.stringify(diagnostics)).not.toContain('private-value');
  expect(JSON.stringify(diagnostics)).not.toContain('baggage');
});

test('uncaught browser failures emit sanitized error spans', async ({ page, applicationUrl }) => {
  const diagnostics = collectBrowserDiagnostics(page);
  await page.goto(applicationUrl);

  await page.evaluate(() => {
    window.dispatchEvent(new ErrorEvent('error', {
      message: 'private grocery value',
      error: new Error('private grocery value'),
    }));
    window.dispatchEvent(new PromiseRejectionEvent('unhandledrejection', {
      promise: Promise.resolve(),
      reason: 'private credential value',
    }));
  });

  await expect.poll(() => diagnostics.map((diagnostic) => diagnostic.span.name)).toEqual(
    expect.arrayContaining(['browser.uncaught_error', 'browser.unhandled_rejection']),
  );
  expect(JSON.stringify(diagnostics)).not.toContain('private grocery value');
  expect(JSON.stringify(diagnostics)).not.toContain('private credential value');
});
