import { createPinia } from 'pinia';
import { describe, expect, it } from 'vitest';
import { useAccessControl } from '@/composables/useAccessControl';
import { Permissions, type StatsFeatureFlags } from '@/api-access/generated/models';

describe('useAccessControl', () => {
  it('returns true when the requested feature flag is enabled', () => {
    const pinia = createPinia();
    const { configStore, featureFlags } = useAccessControl(pinia);
    const statsFeatureFlags: StatsFeatureFlags = { source: 'Stats', enabled: true };

    configStore.config = { featureFlags: { Stats: statsFeatureFlags } };

    expect(featureFlags.value.Stats?.enabled).toBe(true);
  });

  it('returns false when the requested feature flag is disabled', () => {
    const pinia = createPinia();
    const { configStore, featureFlags } = useAccessControl(pinia);
    const statsFeatureFlags: StatsFeatureFlags = { source: 'Stats', enabled: false };

    configStore.config = { featureFlags: { Stats: statsFeatureFlags } };

    expect(featureFlags.value.Stats?.enabled).toBe(false);
  });

  it('returns empty object when config is not loaded', () => {
    const pinia = createPinia();
    const { configStore, featureFlags } = useAccessControl(pinia);

    configStore.config = null;

    expect(featureFlags.value).toEqual({});
  });

  it('returns true if user has a permission', () => {
    const pinia = createPinia();
    const { authStore, hasPermission } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit],
      userId: null,
      homeLocationId: null,
    };
    expect(hasPermission(Permissions.RolesEdit)).toBe(true);
  });

  it('returns false if user does not have a permission', () => {
    const pinia = createPinia();
    const { authStore, hasPermission } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit],
      userId: null,
      homeLocationId: null,
    };
    expect(hasPermission(Permissions.UsersCreate)).toBe(false);
  });

  it('returns true if user has any of the permissions', () => {
    const pinia = createPinia();
    const { authStore, hasAnyPermission } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit],
      userId: null,
      homeLocationId: null,
    };
    expect(hasAnyPermission(Permissions.UsersCreate, Permissions.RolesEdit)).toBe(true);
  });

  it('returns false if user has none of the permissions', () => {
    const pinia = createPinia();
    const { authStore, hasAnyPermission } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit],
      userId: null,
      homeLocationId: null,
    };
    expect(hasAnyPermission(Permissions.UsersCreate, Permissions.UsersEdit)).toBe(false);
  });

  it('returns true if user has all of the permissions', () => {
    const pinia = createPinia();
    const { authStore, hasAllPermissions } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit, Permissions.UsersCreate],
      userId: null,
      homeLocationId: null,
    };
    expect(hasAllPermissions(Permissions.RolesEdit, Permissions.UsersCreate)).toBe(true);
  });

  it('returns false if user is missing any required permission', () => {
    const pinia = createPinia();
    const { authStore, hasAllPermissions } = useAccessControl(pinia);
    authStore.userInfo = {
      isAuthenticated: true,
      isRegistered: true,
      name: 'Test User',
      authenticationType: 'test',
      claims: [],
      permissions: [Permissions.RolesEdit],
      userId: null,
      homeLocationId: null,
    };
    expect(hasAllPermissions(Permissions.RolesEdit, Permissions.UsersCreate)).toBe(false);
  });
});
