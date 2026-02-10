import { Modules } from '@/stores/config';

export const usersRoutes = [
  {
    path: '/users',
    children: [
      {
        path: '',
        name: 'Users',
        component: () => import('./User.vue'),
        meta: {
          title: 'Users',
        },
      },
    ],
    meta: {
      module: Modules.users,
    },
  },
];
