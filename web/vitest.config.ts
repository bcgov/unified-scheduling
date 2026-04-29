import vue from '@vitejs/plugin-vue';
import { fileURLToPath, URL } from 'node:url';
import { transformAssetUrls } from 'vite-plugin-vuetify';
import { configDefaults, defineConfig } from 'vitest/config';

// Standalone vitest config — only includes plugins and aliases needed for tests.
// Dev-server config, proxy, and dev-only plugins (devtools) are intentionally omitted.
// vite-plugin-vuetify is excluded here: like vueDevTools(), it attempts to establish server
// connections on instantiation which hangs vitest workers. Components are registered globally
// via createVuetify({ components, directives }) in createTestApp(), so auto-import is not needed.
// TODO: Previously used mergeConfig(viteConfig, ...) with happy-dom, which worked but ran in ~160s.
// This config decouples vitest from the dev vite config for a significantly faster test run.
export default defineConfig({
  plugins: [vue({ template: { transformAssetUrls } })],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  test: {
    globals: true,
    pool: 'forks',
    setupFiles: ['./src/__tests__/helpers/setup.ts'],
    environment: 'happy-dom',
    server: {
      deps: {
        inline: ['vuetify'],
      },
    },
    css: false,
    exclude: [...configDefaults.exclude, 'e2e/**'],
    root: fileURLToPath(new URL('./', import.meta.url)),
  },
});
