/// <reference types="vitest/config" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  resolve: {
    // Keep the path Vite was started from instead of resolving links. scripts/vite.mjs starts it through a
    // '#'-free junction, because Vite can't work under a path containing '#' (TASKS.md #68).
    preserveSymlinks: true,
  },
  server: {
    // The API's CORS policy allows AppSettings:FrontendUrl, which is http://localhost:5173 in Development
    port: 5173,
    strictPort: true,
  },
  test: {
    environment: 'jsdom',
    globals: true,
    // No setupFiles: Vitest resolves them to their real path, which brings the '#' back. DOM tests import
    // src/test/utils, and that loads src/test/setup.ts.
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
