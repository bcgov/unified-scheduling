import { mdiAccountGroup, mdiCalendarMonth, mdiChartBar, mdiCog } from '@mdi/js';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import type { DashboardTileId } from '@/branding/types';
import { defaultBrand } from '@/branding/brandingConfig';
import type { RouteLocationRaw } from 'vue-router';
import { useNavigationStore } from '@/stores/NavigationStore';
import { useAccessControl } from '@/composables/useAccessControl';

export interface DashboardTile {
  id: DashboardTileId;
  name: string;
  description: string;
  path: RouteLocationRaw;
  icon: string;
  /** All listed feature flags must be enabled for this tile. */
  featureFlagKeys: (keyof FeatureFlagsResponse)[];
  /** At least one listed permission is required, when provided. */
  permissions?: Permissions[];
}

/**
 * Available module cards. Add a definition here and include its ID in a
 * brand's dashboardTiles to display it, subject to its access requirements.
 */
const NON_ADMIN_DASHBOARD_TILES: DashboardTile[] = [
  {
    id: 'myTeam',
    name: 'My Team',
    description:
      'View and manage staff information for your selected location, including leave, training and other staff-related details, or update your own profile.',
    path: '/myteam',
    icon: mdiAccountGroup,
    featureFlagKeys: ['UserManagement'],
    permissions: [Permissions.UsersView],
  },
  {
    id: 'stats',
    name: 'Stats',
    description: 'Enter, review and sign off on work-related statistics for yourself or your team members.',
    path: '/stats',
    icon: mdiChartBar,
    featureFlagKeys: ['Stats'],
  },
  {
    id: 'schedule',
    name: 'Schedule',
    description:
      'Manage, edit and publish schedules, shifts, assignments, leave, training and loans for your team at your selected location.',
    path: '/calendar',
    icon: mdiCalendarMonth,
    featureFlagKeys: ['Calendar', 'Scheduling', 'Leave'],
    permissions: [Permissions.ShiftsView, Permissions.AssignmentsView, Permissions.LeaveView],
  },
];

function getAdminDashboardTile(): DashboardTile | undefined {
  // adminLinks already applies each destination's feature flag and permission checks.
  const destination = useNavigationStore().adminLinks[0];
  if (!destination?.path) return undefined;

  return {
    id: 'admin',
    name: 'Admin',
    description: 'Manage Roles & Permissions, Training Types, Reports, and run and review Audit reports.',
    path: destination.path,
    icon: mdiCog,
    featureFlagKeys: [],
  };
}

export function getDashboardTiles(tileIds?: DashboardTileId[]): DashboardTile[] {
  const selectedTileIds = tileIds ?? defaultBrand.dashboardTiles ?? [];
  const accessControl = useAccessControl();

  return selectedTileIds.flatMap((tileId) => {
    if (tileId === 'admin') {
      const tile = getAdminDashboardTile();
      return tile ? [tile] : [];
    }

    const tile = NON_ADMIN_DASHBOARD_TILES.find((tile) => tile.id === tileId);
    if (!tile) return [];

    const featureEnabled = tile.featureFlagKeys.every((key) => accessControl.featureFlags.value[key]?.enabled);
    const hasPermission = !tile.permissions?.length || accessControl.hasAnyPermission(...tile.permissions);

    return featureEnabled && hasPermission ? [tile] : [];
  });
}
