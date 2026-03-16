import type { RouteRecordRaw } from 'vue-router';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';

const myTeamRoutes: RouteRecordRaw[] = [
  {
    path: '/myteam',
    children: [
      {
        path: '',
        name: 'MyTeam',
        component: () => import('./views/Myteam.vue'),
        meta: {
          title: 'My team',
        },
      },
      {
        path: ':userId',
        name: 'UserProfile',
        component: () => import('./views/UserProfile.vue'),
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
      module: 'myTeamsModule',
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'My Team', path: '/myteam', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[]) {
  const navigationStore = useNavigationStore();

  routes.push(...myTeamRoutes);

  navigationStore.registerLink(navLink);
}
