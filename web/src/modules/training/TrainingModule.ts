import type { RouteRecordRaw } from 'vue-router';
import type { FeatureFlagsResponse } from '@/api-access/generated/models';
import { defineAsyncComponent } from 'vue';
import { type NavigationLink, useNavigationStore } from '@/stores/NavigationStore';
import { calendarActionRegistry } from '@/modules/calendar/registry/calendarActionRegistry';
import { calendarRegistry } from '@/modules/calendar/registry/calendarRegistry';
import { calendarTrainingDetailAction } from './calendarTrainingDetailAction';
import { calendarTrainingEventsContribution } from './contributions/calendarTrainingEventsContribution';

let isCalendarContributionRegistered = false;

const trainingRoutes: RouteRecordRaw[] = [
  {
    path: '/training',
    name: 'Training',
    component: () => import('./Training.vue'),
    meta: {
      title: 'Training',
      requiresAuth: true,
    },
  },
];

const navLink: NavigationLink = { name: 'Training', path: '/training', class: 'router-link--border' };

export function registerModule(routes: RouteRecordRaw[], featureFlags: FeatureFlagsResponse) {
  if (!featureFlags.Training?.enabled) {
    return;
  }

  const navigationStore = useNavigationStore();

  routes.push(...trainingRoutes);

  navigationStore.registerLink(navLink);

  if (featureFlags.Calendar?.enabled && !isCalendarContributionRegistered) {
    calendarRegistry.registerModuleContribution(calendarTrainingEventsContribution);
    calendarRegistry.registerEventDetailComponent(
      'training',
      defineAsyncComponent(() => import('./components/CalendarTrainingDetailModal.vue')),
    );
    calendarActionRegistry.registerMatrixCellHeaderAction(calendarTrainingDetailAction);
    isCalendarContributionRegistered = true;
  }
}
