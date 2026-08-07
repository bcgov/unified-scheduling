import type { RouteRecordRaw } from 'vue-router';
import type { FeatureFlagsResponse } from '@/api-access/generated/models';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';

const trainingRoutes: RouteRecordRaw[] = [
  {
    path: '/training',
    children: [
      {
        path: '',
        name: 'Training',
        component: () => import('./Training.vue'),
        meta: {
          title: 'Training',
        },
      },
    ],
    meta: {
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'Training', path: '/training', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[], featureFlags: FeatureFlagsResponse) {
  if (!featureFlags.Training?.enabled) {
    return;
  }

  const navigationStore = useNavigationStore();

  routes.push(...trainingRoutes);

  navigationStore.registerLink(navLink);
}
