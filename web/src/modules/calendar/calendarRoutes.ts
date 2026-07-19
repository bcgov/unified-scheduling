import type { RouteRecordRaw } from 'vue-router';

export const calendarRoutes: RouteRecordRaw[] = [
  {
    path: '/calendar',
    children: [
      {
        path: '',
        name: 'Calendar',
        component: () => import('./Calendar.vue'),
        meta: {
          title: 'Calendar',
        },
      },
    ],
    meta: {
      module: 'calendarModule',
      requiresAuth: true,
    },
  },
];
