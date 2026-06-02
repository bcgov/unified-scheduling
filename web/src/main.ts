import '@bcgov/design-tokens/css-prefixed/variables.css';

import { createPinia } from 'pinia';
import { createApp } from 'vue';

import { setupMockServiceWorker } from '@/mocks';
import vuetify from '@/plugins/vuetify';
import { initializeRouter } from '@/router';
import { useConfigStore } from '@/stores/config';

import '@/assets/styles/variables.css';

import App from './App.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { getApiAuthUser } from '@/api-access/generated/auth/auth';
import { useAuthStore } from '@/stores/auth';

const bootstrap = async () => {
  await setupMockServiceWorker();

  const pinia = createPinia();

  // @NOTE: Load config before initializing the router to ensure feature flags are available for route setup
  const configStore = useConfigStore(pinia);
  await configStore.loadConfig();

  // @NOTE: Load user info to ensure permissions are available for route guards
  const { error, data: userInfo } = await getApiAuthUser();
  const authStore = useAuthStore(pinia);
  if (!error.value && userInfo.value) {
    authStore.setUserInfo(userInfo.value);
  }

  const app = createApp(App);

  app.use(pinia);

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
