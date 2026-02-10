import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router';
import type { createPinia } from 'pinia';
import { Modules, type ModuleKey } from '@/stores/config';
import { useAccessControl } from '@/composables/useAccessControl';
import { schedulingRoutes } from '@/modules/scheduling/routes';
import { usersRoutes } from '@/modules/users/routes';
import { trainingRoutes } from '@/modules/training/routes';

declare module 'vue-router' {
  interface RouteMeta {
    title?: string;
    // is optional
    module?: ModuleKey;
  }
}

const baseRoutes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/dashboard',
  },
];

const routes: RouteRecordRaw[] = [...baseRoutes];

// Initialize module routes based on access control
export const initializeRouter = (pinia: ReturnType<typeof createPinia>) => {
  const accessControl = useAccessControl(pinia);

  if (accessControl.canAccessModule(Modules.scheduling)) {
    routes.push(...schedulingRoutes);
  }

  if (accessControl.canAccessModule(Modules.users)) {
    routes.push(...usersRoutes);
  }

  if (accessControl.canAccessModule(Modules.training)) {
    routes.push(...trainingRoutes);
  }

  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
  });

  router.beforeEach((to) => {
    const moduleKey = to.meta?.module;

    if (!moduleKey) {
      return true;
    }

    if (!accessControl.canAccessModule(moduleKey)) {
      console.warn(`Access denied to module: ${moduleKey}`);
      return { path: '/dashboard' };
    }

    return true;
  });

  // Update document title on route change
  router.afterEach((to) => {
    document.title = `${to.meta.title || 'Page'} - Unified Scheduling`;
  });

  return router;
};
