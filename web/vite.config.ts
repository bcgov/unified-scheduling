import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import vuetify, { transformAssetUrls } from 'vite-plugin-vuetify';
import vueDevTools from 'vite-plugin-vue-devtools';

const isTest = !!process.env.VITEST;

// https://vite.dev/config/
export default defineConfig({
  base: process.env.WEB_BASE_HREF || '/',
  // Dev-only plugins are excluded during test runs — they attempt to establish
  // server connections on instantiation, which hangs vitest workers.
  plugins: [vue({ template: { transformAssetUrls } }), vuetify(), ...(!isTest ? [vueDevTools()] : [])],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    host: '0.0.0.0',
    port: 8080,
    proxy: {
      '^/api': {
        target: 'http://api:5000',
        changeOrigin: true,
        secure: false,
        headers: {
          Connection: 'keep-alive',
          'X-Forwarded-Host': 'localhost',
          'X-Forwarded-Port': '8080',
          'X-Forwarded-Proto': 'http',
        },
      },
    },
  },
});
