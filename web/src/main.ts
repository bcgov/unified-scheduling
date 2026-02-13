import { createApp } from 'vue';
import { createPinia } from 'pinia';

import { useAuthStore } from '@/stores/auth';
import { useConfigStore } from './stores/config';
import { initializeRouter } from './router';

import App from './App.vue';

const app = createApp(App);

const pinia = createPinia();
app.use(pinia);

const authStore = useAuthStore(pinia);
try {
  await authStore.refreshToken();
} catch (error) {
  console.warn('No valid token found, redirecting to login');
  authStore.login();
  // The login method will redirect the user, so we can return early here
}

const configStore = useConfigStore(pinia);
await configStore.loadConfig();

// Initialize module routes based on access control
const router = initializeRouter(pinia);

app.use(router);

await router.isReady();
app.mount('#app');
