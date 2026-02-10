import { Modules } from '@/stores/config';

export const schedulingRoutes = [
  {
    path: '/scheduling',
    children: [
      {
        path: '',
        name: 'Scheduling',
        component: () => import('./Scheduling.vue'),
        meta: {
          title: 'Scheduling',
        },
      },
    ],
    meta: {
      module: Modules.scheduling,
    },
  }
];
