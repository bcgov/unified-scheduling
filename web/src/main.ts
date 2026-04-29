import '@bcgov/design-tokens/css/variables.css';

import { createPinia } from 'pinia';
import { createApp } from 'vue';

import { setupMockServiceWorker } from '@/mocks';
import vuetify from '@/plugins/vuetify';
import { initializeRouter } from '@/router';
import { useConfigStore } from '@/stores/config';

import '@/assets/styles/variables.css';

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
