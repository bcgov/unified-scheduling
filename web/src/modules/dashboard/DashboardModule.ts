import type { RouteRecordRaw } from 'vue-router';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';

const dashboardRoutes: RouteRecordRaw[] = [
  {
    path: '/dashboard',
    name: 'Dashboard',
    children: [
      {
        path: '',
        name: 'DashboardHome',
        component: () => import('./Dashboard.vue'),
      },
    ],
    meta: {
      title: 'Dashboard',
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'Dashboard', path: '/dashboard' };

export const registerModule = (routes: RouteRecordRaw[]) => {
  const navigationStore = useNavigationStore();

  routes.push(...dashboardRoutes);

  navigationStore.registerLink(navLink);
};
