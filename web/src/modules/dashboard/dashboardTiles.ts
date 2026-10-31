import { mdiAccountOutline, mdiAccountGroup, mdiCalendarMonth, mdiChartBar, mdiCog, mdiSchoolOutline } from '@mdi/js';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import type { DashboardTileId } from '@/branding/types';
import { defaultBrand } from '@/branding/brandingConfig';

export interface DashboardTile {
  id: DashboardTileId;
  name: string;
  description: string;
  path: string;
  icon: string;
  section?: 'personal';
  requiredPermission?: Permissions;
  /** Feature flag key (top-level FeatureFlagsResponse property) that gates this tile. */
  featureFlagKey: keyof FeatureFlagsResponse;
}

/**
 * Available module cards. Add a definition here and include its ID in a
 * brand's dashboardTiles to display it, subject to its feature flag.
 */
const ALL_DASHBOARD_TILES: DashboardTile[] = [
  {
    id: 'myTeam',
    name: 'My Team',
    description:
      'View and manage staff information for your selected location, including leave, training and other staff-related details, or update your own profile.',
    path: '/myteam',
    icon: mdiAccountGroup,
    featureFlagKey: 'UserManagement',
  },
  {
    id: 'stats',
    name: 'Stats',
    description: 'Enter, review and sign off on work-related statistics for yourself or your team members.',
    path: '/stats',
    icon: mdiChartBar,
    featureFlagKey: 'Stats',
  },
  {
    id: 'schedule',
    name: 'Schedule',
    description:
      'Manage, edit and publish schedules, shifts, assignments, leave, training and loans for your team at your selected location.',
    path: '/calendar',
    icon: mdiCalendarMonth,
    featureFlagKey: 'Calendar',
  },
  {
    id: 'admin',
    name: 'Admin',
    description: 'Manage Roles & Permissions, Training Types, Reports, and run and review Audit reports.',
    path: '/roles-permissions',
    icon: mdiCog,
    featureFlagKey: 'UserManagement',
  },
];

export function getDashboardTiles(tileIds?: DashboardTileId[]): DashboardTile[] {
  const selectedTileIds = tileIds ?? defaultBrand.dashboardTiles ?? [];
  return resolveTiles(selectedTileIds);
}

function resolveTiles(selectedTileIds: DashboardTileId[]): DashboardTile[] {
  const tileDefinitions = [...ALL_DASHBOARD_TILES, ...MY_SPACE_TILES];

  return selectedTileIds
    .map((tileId) => tileDefinitions.find((tile) => tile.id === tileId))
    .filter((tile): tile is DashboardTile => !!tile);
}

/**
 * Optional personal destinations for brands that explicitly select them.
 * These appear in the same grid as module cards. The `:userId` placeholder
 * is replaced with the signed-in user's ID before rendering.
 */
const MY_SPACE_TILES: DashboardTile[] = [
  {
    id: 'myProfile',
    section: 'personal',
    name: 'My Profile',
    description: 'View and manage your personal information.',
    path: '/myteam/:userId',
    icon: mdiAccountOutline,
    featureFlagKey: 'UserManagement',
  },
  {
    id: 'training',
    section: 'personal',
    name: 'Training',
    description: 'View your training records and certification status.',
    path: '/myteam/:userId/training',
    icon: mdiSchoolOutline,
    featureFlagKey: 'Training',
  },
  {
    id: 'reporting',
    name: 'Reporting',
    description: 'Generate and view reports.',
    path: '/reports/user-training',
    icon: mdiChartBar,
    featureFlagKey: 'Reporting',
    requiredPermission: Permissions.ReportsGenerate,
  },
];
