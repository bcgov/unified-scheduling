import { fileURLToPath, URL } from 'node:url';
import { defineConfig, configDefaults } from 'vitest/config';
import vue from '@vitejs/plugin-vue';
import vuetify, { transformAssetUrls } from 'vite-plugin-vuetify';

// Standalone vitest config — only includes plugins and aliases needed for tests.
// Dev-server config, proxy, and dev-only plugins (devtools) are intentionally omitted.
// TODO: Previously used mergeConfig(viteConfig, ...) with happy-dom, which worked but ran in ~160s.
// This config decouples vitest from the dev vite config for a significantly faster test run.
export default defineConfig({
  plugins: [vue({ template: { transformAssetUrls } }), vuetify()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    globals: true,
    pool: 'forks',
    setupFiles: ['./src/__tests__/helpers/setup.ts'],
    environment: 'jsdom',
    css: false,
    exclude: [...configDefaults.exclude, 'e2e/**'],
    root: fileURLToPath(new URL('./', import.meta.url)),
  },
});
