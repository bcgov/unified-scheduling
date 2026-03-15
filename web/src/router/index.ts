import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw, RouteLocationNormalized, RouteLocationNormalizedLoaded } from 'vue-router';
import type { createPinia } from 'pinia';
import { useAccessControl } from '@/composables/useAccessControl';
import { schedulingRoutes } from '@/modules/scheduling/routes';
import { myteamRoutes } from '@/modules/myteam/routes';
import { trainingRoutes } from '@/modules/training/routes';
import { dashboardRoutes } from '@/modules/dashboard/routes';
import { useAuthStore } from '@/stores/auth';
import { getApiAuthUser } from '@/api-access/generated/auth/auth';

declare module 'vue-router' {
  interface RouteMeta {
    title?: string;
    module?: string;
    requiresAuth?: boolean;
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

  // If we already have user info, allow navigation
  if (authStore.isAuthenticated) {
    return true;
  }

  try {
    const { data: userInfo } = await getApiAuthUser();

    authStore.setUserInfo(userInfo.value);

    if (userInfo.value?.isAuthenticated) {
      return true;
    } else {
      // Not authenticated — redirect to backend login endpoint.
      // Use the intended destination as returnUrl so the user lands
      // on the correct page after Keycloak SSO completes.
      redirectToLogin(to.fullPath);
      return false;
    }
  } catch {
    // getUserInfo returned 401 — redirect to backend login.
    // Pass the intended destination so the user returns here after SSO.
    redirectToLogin(to.fullPath);
    return false;
  }
}

const AUTH_LOGIN_PATH = '/api/auth/login';

const redirectToLogin = (returnUrl: string) => {
  const redirectUri = encodeURIComponent(returnUrl);
  window.location.href = `${AUTH_LOGIN_PATH}?redirectUri=${redirectUri}`;
};

const baseRoutes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: '/dashboard',
  },
];

const routes: RouteRecordRaw[] = [...baseRoutes, ...dashboardRoutes];

// Initialize module routes based on access control
export const initializeRouter = (pinia: ReturnType<typeof createPinia>) => {
  const accessControl = useAccessControl(pinia);

  if (accessControl.isFeatureFlagEnabled('schedulingModule')) {
    routes.push(...schedulingRoutes);
  }

  if (accessControl.isFeatureFlagEnabled('myteamsModule')) {
    routes.push(...myteamRoutes);
  }

  if (accessControl.isFeatureFlagEnabled('training')) {
    routes.push(...trainingRoutes);
  }

  const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes,
  });

  router.beforeEach(async (to, from) => {
    if (to.meta.requiresAuth) {
      return await authGuard(to, from);
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
