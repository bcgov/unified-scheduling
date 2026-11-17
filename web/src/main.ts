import '@bcgov/design-tokens/css-prefixed/variables.css';
import '@/assets/styles/fonts.css';

import { createPinia } from 'pinia';
import { createApp } from 'vue';

import { setupMockServiceWorker } from '@/mocks';
import { createAppVuetify } from '@/plugins/vuetify';
import { resolveBrandingConfig } from '@/branding/brandingConfig';
import { initializeRouter, LOGGED_OUT_ROUTE_PATH } from '@/router';
import { useConfigStore } from '@/stores/config';
import { redirectToLogin, toApplicationReturnPath } from '@/utils/authNavigation';
import ApplicationUnavailable from '@/views/ApplicationUnavailable.vue';

import '@/assets/styles/variables.css';
import '@/assets/styles/forms.css';

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

  const authStore = useAuthStore(pinia);
  const isLoggedOutPage =
    window.location.pathname.replace(/\/$/, '') === toApplicationReturnPath(LOGGED_OUT_ROUTE_PATH);

  // The logout landing page loads public configuration and the shell without starting authentication.
  if (!isLoggedOutPage) {
    // @NOTE: Load user info to ensure permissions are available for route guards
    const { error, data: userInfo, response } = await getApiAuthUser();
    if (error.value) {
      if (response.value?.status === 401) {
        redirectToLogin();
        return;
      }

      const unavailableApp = createApp(ApplicationUnavailable);
      unavailableApp.mount('#app');
      return;
    }

    if (userInfo.value) {
      authStore.setUserInfo(userInfo.value);
    }
  }

  const app = createApp(App);

  app.use(pinia);

  // Initialize module routes based on access control
  const router = initializeRouter(pinia);

  app.use(router);

  const locationStore = useLocationsStore(pinia);
  if (authStore.isRegistered) {
    await locationStore.getEntities();

    // Seed the selected location from the user's home location
    if (authStore.homeLocationId != null) {
      locationStore.setSelectedLocationId(authStore.homeLocationId);
    }
  }

  // vuetify
  app.use(createAppVuetify(resolveBrandingConfig(configStore.selectedBrand, configStore.config?.brandingOptions)));

  await router.isReady();
  app.mount('#app');
};

void bootstrap();
