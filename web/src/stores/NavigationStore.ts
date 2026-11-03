import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

export interface NavigationLink {
  name: string;
  path: string;
  class?: string;
}

export const useNavigationStore = defineStore('navigation', () => {
  const links = shallowRef<NavigationLink[]>([]);
  const accessControl = useAccessControl();

  const adminPaths: {
    name: string;
    path: string;
    featureFlagKey: keyof FeatureFlagsResponse | null;
    permission: Permissions;
  }[] = [
    {
      name: 'roles-permissions',
      path: '/roles-permissions',
      featureFlagKey: null,
      permission: Permissions.RolesView,
    },
    {
      name: 'training',
      path: '/training',
      featureFlagKey: 'Training',
      permission: Permissions.TrainingsView,
    },
    {
      name: 'reports',
      path: '/reports/user-training',
      featureFlagKey: 'Reporting',
      permission: Permissions.ReportsGenerate,
    },
    {
      name: 'audit',
      path: '/audit',
      featureFlagKey: null,
      permission: Permissions.AuditRead,
    },
  ];

  const adminLinks = computed(() =>
    adminPaths.flatMap((adminPath) => {
      if (
        (adminPath.featureFlagKey == null || accessControl.featureFlags.value[adminPath.featureFlagKey]?.enabled) &&
        (!adminPath.permission || accessControl.hasPermission(adminPath.permission))
      ) {
        const link = links.value.find((link) => link.path === adminPath.path);
        return link ? [link] : [];
      }

      return [];
    }),
  );

  const mainLinks = computed(() => links.value.filter((link) => !adminPaths.some((path) => path.path === link.path)));
  const registerLink = (link: NavigationLink) => {
    links.value = [...links.value, link];
  };
  return { links, mainLinks, adminLinks, adminPaths, registerLink };
});
