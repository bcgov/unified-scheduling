import type {
  CalendarModuleContribution,
  CalendarToolbarAction,
  CalendarViewDetailActionContext,
  CalendarViewDetailAction,
  CalendarViewDefinition,
} from './calendarRegistryTypes';
import type { CalendarQueryContext, CalendarRuntimeContext } from '../calendarTypes';

export class CalendarRegistry {
  private readonly views = new Map<string, CalendarViewDefinition>();
  private readonly contributions = new Map<string, CalendarModuleContribution>();
  private readonly viewDetailActions = new Map<string, CalendarViewDetailAction[]>();

  registerView(view: CalendarViewDefinition) {
    if (this.views.has(view.id)) {
      throw new Error(`Calendar view '${view.id}' is already registered.`);
    }

    this.views.set(view.id, view);
  }

  getAvailableViews(runtimeContext: CalendarRuntimeContext) {
    return [...this.views.values()]
      .filter((view) => view.isAvailable?.(runtimeContext) ?? true)
      .sort((left, right) => (left.order ?? 0) - (right.order ?? 0));
  }

  registerModuleContribution(contribution: CalendarModuleContribution) {
    if (this.contributions.has(contribution.contributionId)) {
      throw new Error(`Calendar contribution '${contribution.contributionId}' is already registered.`);
    }

    this.contributions.set(contribution.contributionId, contribution);
  }

  getAvailableModuleContributions(runtimeContext: CalendarRuntimeContext, queryContext: CalendarQueryContext) {
    return [...this.contributions.values()].filter(
      (contribution) => contribution.isAvailable?.(runtimeContext, queryContext) ?? true,
    );
  }

  getToolbarActionsForView(viewId: string, _context?: CalendarQueryContext): CalendarToolbarAction[] {
    return this.views.get(viewId)?.toolbarActions ?? [];
  }

  registerViewDetailAction(viewId: string, action: CalendarViewDetailAction) {
    const existingActions = this.viewDetailActions.get(viewId) ?? [];

    if (existingActions.some((candidate) => candidate.id === action.id)) {
      throw new Error(`Calendar detail action '${action.id}' is already registered for view '${viewId}'.`);
    }

    this.viewDetailActions.set(viewId, [...existingActions, action]);
  }

  getViewDetailActions(viewId: string, context?: CalendarViewDetailActionContext) {
    const actions = this.viewDetailActions.get(viewId) ?? [];

    if (!context) {
      return actions;
    }

    return actions.filter((action) => action.isAvailable?.(context) ?? true);
  }
}

export const calendarRegistry = new CalendarRegistry();
