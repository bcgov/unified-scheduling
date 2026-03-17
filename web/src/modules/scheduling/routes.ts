import type { RouteRecordRaw } from 'vue-router';
import { Modules } from '@/stores/config';

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
      module: Modules.scheduling,
      requiresAuth: true,
    },
  },
];
