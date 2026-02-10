import { Modules } from '@/stores/config';

export const trainingRoutes = [
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
