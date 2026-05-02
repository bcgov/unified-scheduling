import { fileURLToPath, URL } from 'node:url';

import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import vueDevTools from 'vite-plugin-vue-devtools';
import vuetify, { transformAssetUrls } from 'vite-plugin-vuetify';

const shouldEnableDevTools = process.env.VITEST === undefined;
const devPlugins = shouldEnableDevTools ? [vueDevTools()] : [];

// https://vite.dev/config/
export default defineConfig({
  base: process.env.WEB_BASE_HREF || '/',
  // Dev-only plugins are excluded during test runs — they attempt to establish
  // server connections on instantiation, which hangs vitest workers.
  plugins: [vue({ template: { transformAssetUrls } }), vuetify(), ...devPlugins],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  optimizeDeps: {
    include: [
      'vuetify/components/VAlert',
      'vuetify/components/VApp',
      'vuetify/components/VAppBar',
      'vuetify/components/VAvatar',
      'vuetify/components/VBtn',
      'vuetify/components/VBtnToggle',
      'vuetify/components/VCard',
      'vuetify/components/VDialog',
      'vuetify/components/VIcon',
      'vuetify/components/VProgressCircular',
      'vuetify/components/VSelect',
      'vuetify/components/VSwitch',
      'vuetify/components/VTextField',
      'vuetify/components/VTextarea',
    ],
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
