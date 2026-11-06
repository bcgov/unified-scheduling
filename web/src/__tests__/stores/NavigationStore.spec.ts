import { beforeEach, describe, expect, it } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { Permissions } from '@/api-access/generated/models';
import { useAuthStore } from '@/stores/auth';
import { useConfigStore } from '@/stores/config';
import { useNavigationStore } from '@/stores/NavigationStore';

describe('navigation store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('filters admin links by registered path, feature flag, and permission', () => {
    useConfigStore().config = {
      featureFlags: {
        UserManagement: { enabled: true },
        Training: { enabled: true },
        Reporting: { enabled: false },
      },
    };
    useAuthStore().userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesView, Permissions.ReportsGenerate, Permissions.AuditRead],
      userId: 'test-user',
      homeLocationId: null,
    };

    const navigationStore = useNavigationStore();
    navigationStore.registerLink({ name: 'Roles & Permissions', path: '/roles-permissions' });
    navigationStore.registerLink({ name: 'Training', path: '/training' });
    navigationStore.registerLink({ name: 'Reports', path: '/reports/user-training' });
    navigationStore.registerLink({ name: 'Audit', path: '/audit' });

    expect(navigationStore.adminLinks.map((link) => link.name)).toEqual(['Roles & Permissions', 'Audit']);
  });
});
