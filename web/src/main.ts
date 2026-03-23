import { createApp } from 'vue';
import { createPinia } from 'pinia';

import { setupMockServiceWorker } from '@/mocks';
import { useConfigStore } from '@/stores/config';
import { initializeRouter } from '@/router';
import vuetify from '@/plugins/vuetify';

import App from './App.vue';
import { useLocationsStore } from './stores/LocationsStore';

const bootstrap = async () => {
  const app = createApp(App);

  const pinia = createPinia();
  app.use(pinia);

  await setupMockServiceWorker();

  const configStore = useConfigStore(pinia);
  await configStore.loadConfig();

  // Initialize module routes based on access control
  const router = initializeRouter(pinia);
  app.use(router);

  const locationStore = useLocationsStore(pinia);
  await locationStore.getEntities();

  // vuetify
  app.use(vuetify);

  await router.isReady();
  app.mount('#app');
};

void bootstrap();
