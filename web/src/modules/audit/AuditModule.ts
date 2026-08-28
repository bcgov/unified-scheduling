import type { RouteRecordRaw } from 'vue-router';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';
import { useAccessControl } from '@/composables/useAccessControl';
import { Permissions } from '@/api-access/generated/models';

const auditRoutes: RouteRecordRaw[] = [
  {
    path: '/audit',
    children: [
      {
        path: '',
        name: 'AuditHistory',
        component: () => import('./views/AuditHistoryView.vue'),
        meta: {
          title: 'Audit History',
          requiresAuth: true,
        },
      },
    ],
    meta: {
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'Audit', path: '/audit', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[]) {
  const accessControl = useAccessControl();
  if (!accessControl.hasPermission(Permissions.AuditRead)) {
    return;
  }

  const navigationStore = useNavigationStore();

  routes.push(...auditRoutes);
  navigationStore.registerLink(navLink);
}
