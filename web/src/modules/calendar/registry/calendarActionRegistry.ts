import type { CalendarCreateAction, CalendarCreateContext } from './calendarActionRegistryTypes';
import type { CalendarRuntimeContext } from '../calendarTypes';

class CalendarActionRegistry {
  private readonly createActions = new Map<string, CalendarCreateAction>();

  registerCreateAction(action: CalendarCreateAction) {
    if (this.createActions.has(action.id)) {
      throw new Error(`Calendar create action '${action.id}' is already registered.`);
    }

    this.createActions.set(action.id, action);
  }

  getCreateActions(createContext: CalendarCreateContext, runtimeContext: CalendarRuntimeContext) {
    return [...this.createActions.values()].filter(
      (action) => action.isAvailable?.(createContext, runtimeContext) ?? true,
    );
  }
}

export const calendarActionRegistry = new CalendarActionRegistry();
