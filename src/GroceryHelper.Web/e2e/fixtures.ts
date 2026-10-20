import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { fileURLToPath } from 'node:url';
import { test as base, expect } from '@playwright/test';
import { createServer } from 'vite';

const repositoryRoot = fileURLToPath(new URL('../../../', import.meta.url));
const webRoot = fileURLToPath(new URL('../', import.meta.url));

// Each test owns both servers, including its own backend memory and sessions.
// OS-assigned ports allow parallel tests without reusing developer processes.
export const test = base.extend<{ applicationUrl: string }>({
  // Playwright requires destructuring even when a fixture has no dependencies.
  // eslint-disable-next-line no-empty-pattern
  applicationUrl: async ({}, use, testInfo) => {
    const api = spawn('dotnet', [
      'src/GroceryHelper.Api/bin/Release/net10.0/GroceryHelper.Api.dll',
      '--urls', 'http://127.0.0.1:0',
    ], {
      cwd: repositoryRoot,
      env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Testing', DOTNET_ENVIRONMENT: 'Testing' },
      stdio: ['ignore', 'pipe', 'pipe'],
      windowsHide: true,
    });
    let log = '';
    const exited = once(api, 'exit');
    // Prevent an unhandled rejection if spawning fails.
    void exited.catch(() => {});
    api.stdout.on('data', (chunk: Buffer) => { log += chunk.toString(); });
    api.stderr.on('data', (chunk: Buffer) => { log += chunk.toString(); });

    let web: Awaited<ReturnType<typeof createServer>> | undefined;
    try {
      const apiOrigin = await new Promise<string>((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error(`API startup timed out:\n${log}`)), 15_000);
        const cleanup = () => clearTimeout(timer);
        api.once('error', (error) => { cleanup(); reject(error); });
        api.once('exit', (code) => {
          cleanup();
          reject(new Error(`API exited (${code}):\n${log}`));
        });
        api.stdout.on('data', () => {
          const match = /Now listening on: (http:\/\/127\.0\.0\.1:\d+)/.exec(log);
          if (match) { cleanup(); resolve(match[1]); }
        });
      });
      web = await createServer({
        root: webRoot,
        server: {
          host: '127.0.0.1',
          port: 0,
          proxy: { '/api': apiOrigin, '/health': apiOrigin },
        },
      });
      await web.listen();
      const address = web.httpServer?.address();
      if (!address || typeof address === 'string') throw new Error('No frontend TCP address');
      await use(`http://127.0.0.1:${address.port}`);
    } finally {
      await web?.close();
      if (api.exitCode === null && api.pid) {
        api.kill();
        await exited;
      }
      if (testInfo.status !== testInfo.expectedStatus) {
        await testInfo.attach('api.log', { body: log, contentType: 'text/plain' });
      }
    }
  },
});

export { expect };
