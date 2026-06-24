import type { Pinia } from 'pinia';
import { useConfigStore } from '@/stores/config';
import { useAuthStore } from '@/stores/auth';
import type { FeatureFlags, Permissions } from '@/api-access/generated/models';

export type AppFeatureFlagKey = keyof FeatureFlags;

/**
 * Composable for access control including feature flags and permissions.
 *
 * @param pinia - Optional Pinia instance for testing or multi-instance scenarios
 * @returns Object containing access control methods and stores
 *
 * @example
 * ```ts
 * const accessControl = useAccessControl();
 *
 *  Check if a feature is enabled
 * if (accessControl.isFeatureFlagEnabled('statsModule')) {
 *    render stats feature
 * }
 *
 *  Check for single permission
 * if (accessControl.hasPermission('ShiftsEdit')) {
 *    allow edit action
 * }
 *
 *  Check for any of multiple permissions
 * if (accessControl.hasAnyPermission('ShiftsEdit', 'ShiftsCreateAndAssign')) {
 *    allow if user has at least one
 * }
 *
 *  Check for all permissions
 * if (accessControl.hasAllPermissions('ShiftsEdit', 'RolesView')) {
 *    allow only if user has both
 * }
 * ```
 */
export const useAccessControl = (pinia?: Pinia) => {
  const configStore = useConfigStore(pinia);
  const authStore = useAuthStore(pinia);

  /**
   * Check if user has a specific permission
   *
   * @param permission - The permission string to check (e.g., 'ShiftsEdit')
   * @returns True if user has the permission
   *
   * @example
   * ```ts
   * if (accessControl.hasPermission('ShiftsEdit')) {
   *    user can edit shifts
   * }
   * ```
   */
  const hasPermission = (permission: Permissions): boolean => {
    return authStore.userInfo?.permissions.includes(permission) ?? false;
  };

  /**
   * Check if user has any of the specified permissions
   *
   * @param permissions - Permission strings to check (variadic arguments)
   * @returns True if user has at least one of the permissions
   *
   * @example
   * ```ts
   * if (accessControl.hasAnyPermission('ShiftsEdit', 'ShiftsCreateAndAssign')) {
   *    user can perform at least one of these actions
   * }
   * ```
   */
  const hasAnyPermission = (...permissions: Permissions[]): boolean => {
    return permissions.some((p) => authStore.userInfo?.permissions.includes(p) ?? false);
  };

  /**
   * Check if user has all of the specified permissions
   *
   * @param permissions - Permission strings to check (variadic arguments)
   * @returns True if user has all of the permissions
   *
   * @example
   * ```ts
   * if (accessControl.hasAllPermissions('ShiftsEdit', 'RolesView')) {
   *   user can perform both actions
   * }
   * ```
   */
  const hasAllPermissions = (...permissions: Permissions[]): boolean => {
    return permissions.every((p) => authStore.userInfo?.permissions.includes(p) ?? false);
  };

  /**
   * Check if a feature flag is enabled
   *
   * @param moduleKey - The feature flag key from FeatureFlags
   * @returns True if the feature flag is enabled
   *
   * @example
   * ```ts
   * if (accessControl.isFeatureFlagEnabled('statsModule')) {
   *    feature is available
   * }
   * ```
   */
  const isFeatureFlagEnabled = (moduleKey: AppFeatureFlagKey): boolean => {
    return configStore.config?.featureFlags?.[moduleKey] ?? false;
  };

  return {
    configStore,
    authStore,
    isFeatureFlagEnabled,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
  };
};
