import { readFileSync } from 'node:fs';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const packageMetadata = JSON.parse(
  readFileSync(new URL('./package.json', import.meta.url), 'utf8'),
) as { version: string };

export default defineConfig(({ mode }) => ({
  define: {
    __BUILD_VERSION__: JSON.stringify(process.env.BUILD_VERSION ?? packageMetadata.version),
    __DEPLOYMENT_ENVIRONMENT__: JSON.stringify(
      process.env.DEPLOYMENT_ENVIRONMENT ?? mode,
    ),
  },
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': process.env.API_ORIGIN ?? 'http://127.0.0.1:5080',
      '/health': process.env.API_ORIGIN ?? 'http://127.0.0.1:5080',
    },
  },
}));
