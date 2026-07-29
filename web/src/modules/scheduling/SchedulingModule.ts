import type { RouteRecordRaw } from 'vue-router';
import type { FeatureFlagsResponse } from '@/api-access/generated/models';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';

export const schedulingRoutes: RouteRecordRaw[] = [
  {
    path: '/schedule',
    children: [
      {
        path: '',
        name: 'Schedule',
        component: () => import('./Scheduling.vue'),
        meta: {
          title: 'Schedule',
        },
      },
    ],
    meta: {
      requiresAuth: true,
    },
  },
];

const schedulingNavLink: NavigationLink = {
  name: 'Schedule',
  path: '/schedule',
  class: 'router-link--border',
};

export function registerModule(routes: RouteRecordRaw[], featureFlags: FeatureFlagsResponse) {
  if (!featureFlags.Scheduling?.enabled) {
    return;
  }

  routes.push(...schedulingRoutes);
  useNavigationStore().registerLink(schedulingNavLink);
}
