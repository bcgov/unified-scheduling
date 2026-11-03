import { mdiAccountOutline, mdiAccountGroup, mdiCalendarMonth, mdiChartBar, mdiCog, mdiSchoolOutline } from '@mdi/js';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import type { DashboardTileId } from '@/branding/types';
import { defaultBrand } from '@/branding/brandingConfig';
import { useAuthStore } from '@/stores/auth';
import type { RouteLocationRaw } from 'vue-router';

export interface DashboardTile {
  id: DashboardTileId;
  name: string;
  description: string;
  path: RouteLocationRaw;
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

export function getDashboardTiles(tileIds?: DashboardTileId[]): DashboardTile[] {
  const selectedTileIds = tileIds ?? defaultBrand.dashboardTiles ?? [];
  return resolveTiles(selectedTileIds);
}

function resolveTiles(selectedTileIds: DashboardTileId[]): DashboardTile[] {
  const tileDefinitions = [...ALL_DASHBOARD_TILES, ...getMySpaceTiles()];

  return selectedTileIds
    .map((tileId) => tileDefinitions.find((tile) => tile.id === tileId))
    .filter((tile): tile is DashboardTile => !!tile);
}

/**
 * Optional personal destinations for brands that explicitly select them.
 * These appear in the same grid as module cards and uses authstore to get the signed-in user's ID.
 */
function getMySpaceTiles(): DashboardTile[] {
  const authStore = useAuthStore();

  return [
    {
      id: 'myProfile',
      section: 'personal',
      name: 'My Profile',
      description: 'View and manage your personal information.',
      path: { name: 'UserProfile', params: { userId: authStore.currentUserId ?? undefined } },
      icon: mdiAccountOutline,
      featureFlagKey: 'UserManagement',
    },
    {
      id: 'training',
      section: 'personal',
      name: 'My Training',
      description: 'View your training records and certification status.',
      path: { name: 'UserTraining', params: { userId: authStore.currentUserId ?? undefined } },
      icon: mdiSchoolOutline,
      featureFlagKey: 'Training',
    },
  ];
}
