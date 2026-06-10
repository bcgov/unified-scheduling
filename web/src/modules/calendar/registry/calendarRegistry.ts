import type { CalendarModuleContribution, CalendarViewDefinition } from './calendarRegistryTypes';
import type { CalendarQueryContext, CalendarRuntimeContext } from '../calendarTypes';

export class CalendarRegistry {
  private readonly views = new Map<string, CalendarViewDefinition>();
  private readonly contributions = new Map<string, CalendarModuleContribution>();

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
}

export const calendarRegistry = new CalendarRegistry();
