import type { RouteRecordRaw } from 'vue-router';

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
      module: 'trainingModule',
      requiresAuth: true,
    },
  },
];
