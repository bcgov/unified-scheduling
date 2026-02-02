import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/dashboard',
  },
  {
    path: '/dashboard',
    name: 'Dashboard',
    component: () => import('../modules/dashboard/Dashboard.vue'),
    meta: {
      title: 'Dashboard',
    },
  },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
})

// Update document title on route change
router.afterEach((to) => {
  document.title = `${to.meta.title || 'Page'} - Unified Scheduling`
})

export default router
