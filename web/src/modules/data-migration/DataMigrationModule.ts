import type { RouteRecordRaw } from 'vue-router';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

// Local contract additions until the next full Orval regeneration.
type MigrationFeatureFlags = { DataMigration?: { enabled?: boolean } };

export function canOperateDataMigration(access = useAccessControl()) {
  const flags = (access.featureFlags?.value ?? {}) as MigrationFeatureFlags;
  return (
    flags.DataMigration?.enabled === true &&
    access.authStore?.isAuthenticated === true &&
    access.authStore.isRegistered &&
    access.hasPermission?.(Permissions.DataMigrationOperate) === true
  );
}

export function registerModule(routes: RouteRecordRaw[]) {
  if (!canOperateDataMigration()) return;

  routes.push({
    path: '/data-migration',
    name: 'DataMigration',
    component: () => import('./DataMigration.vue'),
    meta: { title: 'Data Migration', requiresAuth: true },
    beforeEnter: () => (canOperateDataMigration() ? true : { path: '/dashboard' }),
  });
}
