import { createApp } from 'vue';
import { createPinia } from 'pinia';

import 'vuetify/styles';
import { createVuetify } from 'vuetify';

import { initializeAuthSession } from '@/api-access/authSession';
import { setupMockServiceWorker } from '@/mocks';
import { useConfigStore } from '@/stores/config';
import { initializeRouter } from '@/router';

import App from './App.vue';

const bootstrap = async () => {
  const app = createApp(App);

  const pinia = createPinia();
  app.use(pinia);

  await setupMockServiceWorker();

  const isAuthenticated = await initializeAuthSession();
  if (!isAuthenticated) {
    return;
  }

  const configStore = useConfigStore(pinia);
  await configStore.loadConfig();

  // Initialize module routes based on access control
  const router = initializeRouter(pinia);
  app.use(router);

  // vuetify
  const vuetify = createVuetify();
  app.use(vuetify);

  await router.isReady();
  app.mount('#app');
};

void bootstrap();
