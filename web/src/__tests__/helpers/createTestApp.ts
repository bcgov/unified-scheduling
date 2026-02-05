import { createRouter, createWebHistory } from 'vue-router';
import router from '../../router/index';

/**
 *
 * @see https://alexop.dev/posts/vue3_testing_pyramid_vitest_browser_mode/
 *
 */
export async function createTestApp() {
  // ... setup router, pinia, render app ...


  return {
    router,       // The navigation system
    // cleanup       // A function to tidy up after the test
  }
}
