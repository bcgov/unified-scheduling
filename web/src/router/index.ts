import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw, RouteLocationNormalized, RouteLocationNormalizedLoaded } from 'vue-router';
import type { createPinia } from 'pinia';
import type { FeatureFlagsResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import * as calendarModule from '@/modules/calendar/CalendarModule';
import * as calendarSchedulingModule from '@/modules/scheduling/CalendarSchedulingModule';
import * as myTeamsModule from '@/modules/myteam/MyTeamModule';
import * as dashboardModule from '@/modules/dashboard/DashboardModule';
import * as statsModule from '@/modules/stats/StatsModule';
import * as trainingModule from '@/modules/training/TrainingModule';
import * as reportsModule from '@/modules/reports/ReportsModule';
import * as auditModule from '@/modules/audit/AuditModule';
import { useAuthStore } from '@/stores/auth';
import { getApiAuthUser } from '@/api-access/generated/auth/auth';
import { redirectToLogin } from '@/utils/authNavigation';

const UNREGISTERED_ROUTE_PATH = '/unregistered';
const APPLICATION_UNAVAILABLE_ROUTE_PATH = '/application-unavailable';

declare module 'vue-router' {
  interface RouteMeta {
    title?: string;
    requiresAuth?: boolean;
    fullScreen?: boolean;
  }
}

/**
 * Auth guard that checks authentication before navigating to protected routes.
 *  1. Calls the backend /api/auth/user endpoint
 *  2. If authenticated, stores user info and allows navigation
 *  3. If 401, redirects to /api/auth/login (Keycloak SSO)
 *  4. If authentication cannot be checked, shows the application unavailable route
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
    const { data: userInfo, error, response } = await getApiAuthUser();

    if (error.value) {
      if (response.value?.status === 401) {
        redirectToLogin(to.fullPath);
        return false;
      }

      return { path: APPLICATION_UNAVAILABLE_ROUTE_PATH };
    }

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
    return { path: APPLICATION_UNAVAILABLE_ROUTE_PATH };
  }
}

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
    path: APPLICATION_UNAVAILABLE_ROUTE_PATH,
    name: 'ApplicationUnavailable',
    component: () => import('@/views/ApplicationUnavailable.vue'),
    meta: {
      title: 'Application unavailable',
      fullScreen: true,
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
  const featureFlags = (accessControl.featureFlags?.value ?? {}) as FeatureFlagsResponse;

  dashboardModule.registerModule(routes);
  myTeamsModule.registerModule(routes, featureFlags);
  statsModule.registerModule(routes, featureFlags);
  calendarModule.registerModule(routes, featureFlags);
  if (featureFlags.Scheduling?.enabled) {
    calendarSchedulingModule.registerModule();
  }
  trainingModule.registerModule(routes, featureFlags);
  reportsModule.registerModule(routes, featureFlags);
  auditModule.registerModule(routes);

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

    return true;
  });

  // Update document title on route change
  router.afterEach((to) => {
    document.title = `${to.meta.title || 'Page'} - Unified Scheduling`;
  });

  return router;
};
