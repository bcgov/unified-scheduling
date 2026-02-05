import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'

const app = createApp(App)

app.use(createPinia())
app.use(router)

interface Config {
  modules: string[]
}

const config: Config = await fetch('/src/assets/config.json').then(res => res.json())

app.provide<Config>('config', config)

config.modules.forEach(module => {
  console.log(`Module loaded: ${module}`);

  if (module === 'users') {
    router.addRoute({
      path: '/users',
      name: 'Users',
      component: () => import('./modules/users/Users.vue'),
      meta: {
        title: 'Users',
      },
    });
  }
});

await router.isReady()
app.mount('#app')
