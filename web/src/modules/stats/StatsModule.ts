import type { RouteRecordRaw } from 'vue-router';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';
import { useAuthStore } from '@/stores/auth';
import { DateTime } from 'luxon';

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
        redirect: () => {
          const now = DateTime.now();
          const authStore = useAuthStore();
          return {
            name: 'StatSearch',
            query: {
              source: 'signoffs',
              status: 'Submitted',
              fromDate: now.startOf('month').toISODate(),
              toDate: now.endOf('month').toISODate(),
              ...(authStore.homeLocationId ? { locationId: String(authStore.homeLocationId) } : {}),
            },
          };
        },
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
