import type { RouteRecordRaw } from 'vue-router';
import { useNavigationStore } from '@/stores/NavigationStore';
import { calendarCreateAction } from './actions/calendarCreateAction';
import { calendarEventDetailModalAction } from './actions/calendarEventDetailModalAction';
import { calendarNavLink } from './calendarNavigation';
import { calendarRoutes } from './calendarRoutes';
import { calendarEventsContribution } from './contributions/calendarEventsContribution';
import { calendarActionRegistry } from './registry/calendarActionRegistry';
import { calendarRegistry } from './registry/calendarRegistry';
import { calendarDefaultViewContribution } from './views/calendarDefaultViewContribution';

let isRegistered = false;

export function registerModule(routes: RouteRecordRaw[]) {
  if (isRegistered) {
    return;
  }

  const navigationStore = useNavigationStore();

  routes.push(...calendarRoutes);
  navigationStore.registerLink(calendarNavLink);

  calendarRegistry.registerView(calendarDefaultViewContribution);
  calendarRegistry.registerModuleContribution(calendarEventsContribution);
  calendarRegistry.registerViewDetailAction(calendarDefaultViewContribution.id, calendarEventDetailModalAction);

  calendarActionRegistry.registerCreateAction(calendarCreateAction);

  isRegistered = true;
}
