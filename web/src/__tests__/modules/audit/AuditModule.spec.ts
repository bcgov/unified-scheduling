import { beforeEach, describe, expect, it, vi } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import type { RouteRecordRaw } from 'vue-router';

describe('audit module', () => {
  beforeEach(() => {
    vi.resetModules();
    setActivePinia(createPinia());
  });

  it('registers audit route and navigation link when user has AuditRead permission', async () => {
    const routes: RouteRecordRaw[] = [];

    const [{ registerModule }, { useNavigationStore }, { useAuthStore }] = await Promise.all([
      import('@/modules/audit/AuditModule'),
      import('@/stores/NavigationStore'),
      import('@/stores/auth'),
    ]);

    const authStore = useAuthStore();
    authStore.setUserInfo({
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: ['AuditRead'],
      userId: null,
      homeLocationId: null,
    });

    registerModule(routes);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(1);
    expect(routes[0]?.path).toBe('/audit');
    expect(routes[0]?.meta).toMatchObject({ requiresAuth: true });
    expect(routes[0]?.children?.[0]?.name).toBe('AuditHistory');

    expect(navigationStore.links).toEqual([{ name: 'Audit', path: '/audit', class: 'router-link--border' }]);
  });

  it('does not register audit routes when user lacks AuditRead permission', async () => {
    const routes: RouteRecordRaw[] = [];

    const [{ registerModule }, { useNavigationStore }, { useAuthStore }] = await Promise.all([
      import('@/modules/audit/AuditModule'),
      import('@/stores/NavigationStore'),
      import('@/stores/auth'),
    ]);

    const authStore = useAuthStore();
    authStore.setUserInfo({
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [],
      userId: null,
      homeLocationId: null,
    });

    registerModule(routes);

    const navigationStore = useNavigationStore();

    expect(routes).toHaveLength(0);
    expect(navigationStore.links).toHaveLength(0);
  });
});
