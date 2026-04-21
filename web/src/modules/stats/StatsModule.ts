import type { RouteRecordRaw } from 'vue-router';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';

const statsRoutes: RouteRecordRaw[] = [
  {
    path: '/stats',
    meta: {
      module: 'statsModule',
      requiresAuth: true,
    },
    children: [
      {
        path: '',
        name: 'StatsLanding',
        component: () => import('./views/StatsLandingView.vue'),
        meta: { title: 'Stats' },
      },
      {
        path: 'non-supervision',
        name: 'NonSupervisionForm',
        component: () => import('./views/EnterHoursView.vue'),
        props: { groupId: 1 },
        meta: { title: 'Enter Non-Supervision Hours' },
      },
      {
        path: 'supervision',
        name: 'SupervisionForm',
        component: () => import('./views/EnterHoursView.vue'),
        props: { groupId: 2 },
        meta: { title: 'Enter Supervision Hours' },
      },
      {
        path: 'search',
        name: 'StatSearch',
        component: () => import('./views/StatSearchView.vue'),
        meta: { title: 'Search Stats' },
      },
      {
        path: 'signoffs',
        name: 'StatSignoffs',
        component: () => import('./views/StatSignoffsView.vue'),
        meta: { title: 'Monthly End Sign Offs' },
      },
    ],
  },
];

const navLink: NavigationLink = { name: 'Stats', path: '/stats', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[]) {
  const navigationStore = useNavigationStore();

  routes.push(...statsRoutes);

  navigationStore.registerLink(navLink);
}
