import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw, RouteLocationNormalized, RouteLocationNormalizedLoaded } from 'vue-router';
import type { createPinia } from 'pinia';
import { type AppFeatureFlagKey, useAccessControl } from '@/composables/useAccessControl';
import * as calendarModule from '@/modules/calendar/CalendarModule';
import * as myTeamsModule from '@/modules/myteam/MyTeamModule';
import * as dashboardModule from '@/modules/dashboard/DashboardModule';
import * as statsModule from '@/modules/stats/StatsModule';
import * as trainingModule from '@/modules/training/TrainingModule';
import { useAuthStore } from '@/stores/auth';
import { getApiAuthUser } from '@/api-access/generated/auth/auth';

const AUTH_LOGIN_PATH = '/api/auth/login';
const UNREGISTERED_ROUTE_PATH = '/unregistered';

declare module 'vue-router' {
  interface RouteMeta {
    title?: string;
    module?: AppFeatureFlagKey;
    requiresAuth?: boolean;
    fullScreen?: boolean;
  }
}

/**
 * Auth guard that checks authentication before navigating to protected routes.
 *  1. Calls the backend /api/auth/user endpoint
 *  2. If authenticated, stores user info and allows navigation
 *  3. If 401, HttpService interceptor redirects to /api/auth/login (Keycloak SSO)
 */
async function authGuard(to: RouteLocationNormalized, _from: RouteLocationNormalizedLoaded) {
  const authStore = useAuthStore();

  if (authStore.isAuthenticated) {
    if (authStore.isRegistered || to.path === UNREGISTERED_ROUTE_PATH) {
      return true;
    }

    return { path: UNREGISTERED_ROUTE_PATH };
  }

  try {
    const { data: userInfo } = await getApiAuthUser();

    authStore.setUserInfo(userInfo.value);

    if (userInfo.value?.isAuthenticated) {
      const isRegistered = userInfo.value.isRegistered;
      if (isRegistered || to.path === UNREGISTERED_ROUTE_PATH) {
        return true;
      }

      return { path: UNREGISTERED_ROUTE_PATH };
    }

    redirectToLogin(to.fullPath);
    return false;
  } catch {
    // getUserInfo returned 401 — redirect to backend login.
    // Pass the intended destination so the user returns here after SSO.
    redirectToLogin(to.fullPath);
    return false;
  }
}

const redirectToLogin = (returnUrl: string) => {
  const redirectUri = encodeURIComponent(returnUrl);
  window.location.href = `${AUTH_LOGIN_PATH}?redirectUri=${redirectUri}`;
};

const baseRoutes: RouteRecordRaw[] = [
  {
    path: UNREGISTERED_ROUTE_PATH,
    name: 'UnregisteredUser',
    component: () => import('@/views/UnregisteredUser.vue'),
    meta: {
      title: 'Registration required',
      requiresAuth: true,
    },
  },
  {
    path: '/',
    redirect: '/dashboard',
  },
];

const routes: RouteRecordRaw[] = [...baseRoutes];

// Initialize module routes based on access control
export const initializeRouter = (pinia: ReturnType<typeof createPinia>) => {
  const accessControl = useAccessControl(pinia);

  dashboardModule.registerModule(routes);

  if (accessControl.isFeatureFlagEnabled('myTeamsModule')) {
    myTeamsModule.registerModule(routes);
  }

  if (accessControl.isFeatureFlagEnabled('statsModule')) {
    statsModule.registerModule(routes);
  }

  if (accessControl.isFeatureFlagEnabled('calendarModule')) {
    calendarModule.registerModule(routes);
  }

  if (accessControl.isFeatureFlagEnabled('trainingModule')) {
    trainingModule.registerModule(routes);
  }

  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
  });

  router.beforeEach(async (to, from) => {
    if (to.meta.requiresAuth) {
      const authResult = await authGuard(to, from);
      if (authResult !== true) {
        return authResult;
      }
    }

    const authStore = useAuthStore();
    if (to.path === UNREGISTERED_ROUTE_PATH && authStore.isAuthenticated && authStore.isRegistered) {
      return { path: '/dashboard' };
    }

    const moduleKey = to.meta?.module;

    if (!moduleKey) {
      return true;
    }

    if (!accessControl.isFeatureFlagEnabled(moduleKey)) {
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
