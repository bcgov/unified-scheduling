import type { RouteRecordRaw } from 'vue-router';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';
import { useAccessControl } from '@/composables/useAccessControl';

const reportsRoutes: RouteRecordRaw[] = [
  {
    path: '/reports/user-training',
    name: 'UserTrainingReport',
    component: () => import('@/modules/reports/UserTrainingReport.vue'),
    meta: {
      title: 'User Training Report',
      requiresAuth: true,
    },
  },
  {
    path: '/training/reports/user-training',
    redirect: { name: 'UserTrainingReport' },
    meta: {
      requiresAuth: true,
    },
  },
];

const reportsNavLink: NavigationLink = {
  name: 'Reports',
  path: '/reports/user-training',
  class: 'router-link--border',
};

export function registerModule(routes: RouteRecordRaw[], _featureFlags: FeatureFlagsResponse) {
  const navigationStore = useNavigationStore();
  const accessControl = useAccessControl();

  routes.push(...reportsRoutes);

  if (accessControl.hasPermission(Permissions.ReportsGenerate)) {
    navigationStore.registerLink(reportsNavLink);
  }
}
