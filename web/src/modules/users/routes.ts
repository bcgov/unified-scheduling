import type { RouteRecordRaw } from 'vue-router';
import { Modules } from '@/stores/config';

export const usersRoutes: RouteRecordRaw[] = [
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
      {
        path: ':userId/profile',
        name: 'UserProfile',
        component: () => import('./UserProfile.vue'),
        meta: {
          title: 'User Profile',
        },
        props: true,
      },
    ],
    meta: {
      module: Modules.users,
      requiresAuth: true,
    },
  },
];
