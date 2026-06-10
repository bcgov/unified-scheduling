import type {
  CalendarCreateAction,
  CalendarCreateContext,
  CalendarToolbarAction,
  CalendarViewDetailAction,
  CalendarViewDetailActionContext,
} from './calendarActionRegistryTypes';
import type { CalendarQueryContext, CalendarRuntimeContext } from '../calendarTypes';

export class CalendarActionRegistry {
  private readonly createActions = new Map<string, CalendarCreateAction>();
  private readonly toolbarActionsByView = new Map<string, CalendarToolbarAction[]>();
  private readonly viewDetailActionsByView = new Map<string, CalendarViewDetailAction[]>();

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

  registerToolbarAction(viewId: string, action: CalendarToolbarAction) {
    const existingActions = this.toolbarActionsByView.get(viewId) ?? [];

    if (existingActions.some((candidate) => candidate.id === action.id)) {
      throw new Error(`Calendar toolbar action '${action.id}' is already registered for view '${viewId}'.`);
    }

    this.toolbarActionsByView.set(viewId, [...existingActions, action]);
  }

  getToolbarActionsForView(viewId: string, _context?: CalendarQueryContext): CalendarToolbarAction[] {
    return this.toolbarActionsByView.get(viewId) ?? [];
  }

  registerViewDetailAction(viewId: string, action: CalendarViewDetailAction) {
    const existingActions = this.viewDetailActionsByView.get(viewId) ?? [];

    if (existingActions.some((candidate) => candidate.id === action.id)) {
      throw new Error(`Calendar detail action '${action.id}' is already registered for view '${viewId}'.`);
    }

    this.viewDetailActionsByView.set(viewId, [...existingActions, action]);
  }

  getViewDetailActions(viewId: string, context?: CalendarViewDetailActionContext) {
    const actions = this.viewDetailActionsByView.get(viewId) ?? [];

    if (!context) {
      return actions;
    }

    return actions.filter((action) => action.isAvailable?.(context) ?? true);
  }
}

export const calendarActionRegistry = new CalendarActionRegistry();
