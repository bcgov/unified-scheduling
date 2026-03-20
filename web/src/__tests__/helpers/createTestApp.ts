import { createPinia } from 'pinia';
import { initializeRouter } from '../../router/index';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import { getGetApiConfigMockHandler, getGetApiConfigResponseMock } from '@/api-access/generated/config/config.msw';
import type { FeatureFlags } from '@/api-access/generated/models';
import { useConfigStore } from '@/stores/config';
import { useAuthStore } from '@/stores/auth';
import { server } from '../mocks/server';

const vuetify = createVuetify({
  components,
  directives,
});

interface CreateTestAppOptions {
  featureFlags?: Partial<FeatureFlags>;
  loadConfig?: boolean;
  isAuthenticated?: boolean;
}
// Generate default config response, then override with any specified in createTestApp options.
const configResponse = getGetApiConfigResponseMock();

/**
 *
 * @see https://alexop.dev/posts/vue3_testing_pyramid_vitest_browser_mode/
 *
 */
export async function createTestApp(options: CreateTestAppOptions = {}) {
  // ... setup router, pinia, render app ...
  const pinia = createPinia();

  const authStore = useAuthStore(pinia);
  authStore.setUserInfo({
    isAuthenticated: options.isAuthenticated ?? true,
    name: 'Unit Test User',
    authenticationType: 'test',
    claims: [],
  });

  if (options.loadConfig !== false) {
    const defaultFeatureFlags = configResponse.featureFlags;

    configResponse.featureFlags = {
      ...defaultFeatureFlags,
      ...options.featureFlags,
    };

    server.use(getGetApiConfigMockHandler(configResponse));

    const configStore = useConfigStore(pinia);
    await configStore.loadConfig();
  }

  const router = initializeRouter(pinia);

  return {
    pinia,
    router, // The navigation system
    vuetify,
    mountPlugins: [pinia, router, vuetify],
    // cleanup       // A function to tidy up after the test
  };
}
