import type { RouteRecordRaw } from 'vue-router';

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
      module: 'scheduling',
      requiresAuth: true,
    },
  },
];
