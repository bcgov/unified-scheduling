import type { RouteRecordRaw } from 'vue-router';

export const dashboardRoutes: RouteRecordRaw[] = [
  {
    path: '/dashboard',
    name: 'Dashboard',
    children: [{
      path: '',
      name: 'DashboardHome',
      component: () => import('./Dashboard.vue'),
    }],
    meta: {
      title: 'Dashboard',
    },
  },
]
