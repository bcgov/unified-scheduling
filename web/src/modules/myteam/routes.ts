import type { RouteRecordRaw } from 'vue-router';

export const myteamRoutes: RouteRecordRaw[] = [
  {
    path: '/myteam',
    children: [
      {
        path: '',
        name: 'MyTeam',
        component: () => import('./Myteam.vue'),
        meta: {
          title: 'My team',
        },
      },
      {
        path: ':userId',
        name: 'UserProfile',
        component: () => import('./UserProfile.vue'),
        redirect: (to) => ({
          name: 'UserIdentification',
          params: to.params,
        }),
        meta: {
          title: 'User Profile',
        },
        props: true,
        children: [
          {
            path: 'identification',
            name: 'UserIdentification',
            component: () => import('./components/Identification.vue'),
            meta: {
              title: 'User Identification',
            },
          },
        ],
      },
    ],
    meta: {
      module: 'myteam',
      requiresAuth: true,
    },
  },
];
