import { createPinia } from 'pinia';
import { initializeRouter } from '../../router/index';
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'

const pinia = createPinia();
const router = initializeRouter(pinia);
const vuetify = createVuetify({
  components,
  directives,
});

/**
 *
 * @see https://alexop.dev/posts/vue3_testing_pyramid_vitest_browser_mode/
 *
 */
export async function createTestApp() {
  // ... setup router, pinia, render app ...

  return {
    router, // The navigation system
    vuetify,
    // cleanup       // A function to tidy up after the test
  };
}
