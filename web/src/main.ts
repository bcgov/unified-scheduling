import { createApp } from 'vue';
import { createPinia } from 'pinia';

import { useConfigStore } from './stores/config';
import { initializeRouter } from './router';

import App from './App.vue';


const app = createApp(App);

const pinia = createPinia();
app.use(pinia);

const configStore = useConfigStore(pinia);
await configStore.loadConfig();

// Initialize module routes based on access control
const router = initializeRouter(pinia);

app.use(router);

await router.isReady();
app.mount('#app');
