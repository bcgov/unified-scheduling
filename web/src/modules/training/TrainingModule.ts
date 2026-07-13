import type { RouteRecordRaw } from 'vue-router';
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
      module: 'trainingModule',
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'Training', path: '/training', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[]) {
  const navigationStore = useNavigationStore();

  routes.push(...trainingRoutes);

  navigationStore.registerLink(navLink);
}
