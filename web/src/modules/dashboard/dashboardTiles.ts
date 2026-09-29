import {
  mdiAccountOutline,
  mdiCalendarMonth,
  mdiClipboardTextClockOutline,
  mdiFileDocumentEditOutline,
  mdiSchoolOutline,
  mdiClipboardCheckOutline,
  mdiChartBoxOutline,
} from '@mdi/js';
import type { FeatureFlagsResponse } from '@/api-access/generated/models';

export interface DashboardTile {
  name: string;
  description: string;
  path: string;
  icon: string;
  /** Feature flag key (top-level FeatureFlagsResponse property) that gates this tile. */
  featureFlagKey: keyof FeatureFlagsResponse;
}

/**
 * Data-driven Quick Links shown on the dashboard. Add an entry here when a new
 * UOps module should appear as a quick link — the dashboard grid picks it up
 * automatically and gates it on the corresponding feature flag, with no
 * changes needed to Dashboard.vue.
 */
export const dashboardTiles: DashboardTile[] = [
  {
    name: 'Calendar',
    description: 'View and manage shift schedules and events.',
    path: '/calendar',
    icon: mdiCalendarMonth,
    featureFlagKey: 'Calendar',
  },
  {
    name: 'Scheduling',
    description: 'Assign resources and manage shift assignments.',
    path: '/calendar',
    icon: mdiClipboardTextClockOutline,
    featureFlagKey: 'Scheduling',
  },
  {
    name: 'Search/View/Edit Data',
    description: 'Search, view, and edit submitted stats data.',
    path: '/stats/search',
    icon: mdiFileDocumentEditOutline,
    featureFlagKey: 'Stats',
  },
  {
    name: 'Upcoming Sign-off',
    description: 'Review and sign off on monthly stats submissions.',
    path: '/stats/signoffs',
    icon: mdiClipboardCheckOutline,
    featureFlagKey: 'Stats',
  },
  {
    name: 'Reports',
    description: 'Generate and view user training reports.',
    path: '/reports/user-training',
    icon: mdiChartBoxOutline,
    featureFlagKey: 'Reporting',
  },
];

/**
 * Single vertical "My Schedule" card shown on the right side of the
 * dashboard, linking the signed-in user to their calendar/shift schedule.
 */
export const myScheduleCard: DashboardTile = {
  name: 'My Schedule',
  description: 'View your upcoming shifts and schedule.',
  path: '/calendar',
  icon: mdiCalendarMonth,
  featureFlagKey: 'Calendar',
};

/**
 * Data-driven "My Space" cards shown on the dashboard for the signed-in
 * user's own profile and training record. The `:userId` placeholder in a
 * tile's path is replaced with the current user's id before rendering.
 */
export const mySpaceTiles: DashboardTile[] = [
  {
    name: 'My Profile',
    description: 'View and manage your personal information.',
    path: '/myteam/:userId',
    icon: mdiAccountOutline,
    featureFlagKey: 'UserManagement',
  },
  {
    name: 'Training',
    description: 'View your training records and certification status.',
    path: '/myteam/:userId/training',
    icon: mdiSchoolOutline,
    featureFlagKey: 'Training',
  },
];
