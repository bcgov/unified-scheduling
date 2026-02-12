import type { RouteRecordRaw } from 'vue-router';
import { Modules } from '@/stores/config';

export const trainingRoutes: RouteRecordRaw[] = [
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
      module: Modules.training,
    },
  },
];
