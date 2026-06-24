import type {
  CalendarDropAction,
  CalendarCreateAction,
  CalendarCreateContext,
  CalendarMatrixCellHeaderAction,
  CalendarMatrixCellHeaderActionContext,
  CalendarMatrixEventBlockAction,
  CalendarMatrixEventBlockActionContext,
  CalendarMatrixResourceAction,
  CalendarMatrixResourceActionContext,
  CalendarMatrixSidePanelAction,
  CalendarMatrixSidePanelActionContext,
  CalendarToolbarAction,
  CalendarViewDetailAction,
  CalendarViewDetailActionContext,
} from './calendarActionRegistryTypes';
import type { CalendarQueryContext, CalendarRuntimeContext } from '../calendarTypes';
import type {
  CalendarMatrixCellDropContext,
  CalendarMatrixDragPayload,
} from '../components/matrix/calendarMatrixTypes';

export class CalendarActionRegistry {
  private readonly createActions = new Map<string, CalendarCreateAction>();
  private readonly toolbarActionsByView = new Map<string, CalendarToolbarAction[]>();
  private readonly viewDetailActionsByView = new Map<string, CalendarViewDetailAction[]>();
  private readonly dropActions = new Map<string, CalendarDropAction>();
  private readonly matrixSidePanelActions = new Map<string, CalendarMatrixSidePanelAction>();
  private readonly matrixResourceActions = new Map<string, CalendarMatrixResourceAction>();
  private readonly matrixCellHeaderActions = new Map<string, CalendarMatrixCellHeaderAction>();
  private readonly matrixEventBlockActions = new Map<string, CalendarMatrixEventBlockAction>();

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

  registerDropAction(action: CalendarDropAction) {
    this.registerUniqueAction(this.dropActions, action, 'drop');
  }

  getDropActions(
    drag: CalendarMatrixDragPayload,
    drop: CalendarMatrixCellDropContext,
    runtimeContext: CalendarRuntimeContext,
  ) {
    return this.sortActions([...this.dropActions.values()]).filter(
      (action) => action.isAvailable?.(drag, drop, runtimeContext) ?? true,
    );
  }

  registerMatrixSidePanelAction(action: CalendarMatrixSidePanelAction) {
    this.registerUniqueAction(this.matrixSidePanelActions, action, 'matrix side panel');
  }

  getMatrixSidePanelActions(context: CalendarMatrixSidePanelActionContext, runtimeContext: CalendarRuntimeContext) {
    return this.getMatrixActions(this.matrixSidePanelActions, context.actionId, context, runtimeContext);
  }

  registerMatrixResourceAction(action: CalendarMatrixResourceAction) {
    this.registerUniqueAction(this.matrixResourceActions, action, 'matrix resource');
  }

  getMatrixResourceActions(context: CalendarMatrixResourceActionContext, runtimeContext: CalendarRuntimeContext) {
    return this.getMatrixActions(this.matrixResourceActions, context.actionId, context, runtimeContext);
  }

  registerMatrixCellHeaderAction(action: CalendarMatrixCellHeaderAction) {
    this.registerUniqueAction(this.matrixCellHeaderActions, action, 'matrix cell header');
  }

  getMatrixCellHeaderActions(context: CalendarMatrixCellHeaderActionContext, runtimeContext: CalendarRuntimeContext) {
    return this.getMatrixActions(this.matrixCellHeaderActions, context.actionId, context, runtimeContext);
  }

  registerMatrixEventBlockAction(action: CalendarMatrixEventBlockAction) {
    this.registerUniqueAction(this.matrixEventBlockActions, action, 'matrix event block');
  }

  getMatrixEventBlockActions(context: CalendarMatrixEventBlockActionContext, runtimeContext: CalendarRuntimeContext) {
    return this.getMatrixActions(this.matrixEventBlockActions, context.actionId, context, runtimeContext);
  }

  private registerUniqueAction<TAction extends { id: string }>(
    registry: Map<string, TAction>,
    action: TAction,
    actionKind: string,
  ) {
    if (registry.has(action.id)) {
      throw new Error(`Calendar ${actionKind} action '${action.id}' is already registered.`);
    }

    registry.set(action.id, action);
  }

  private getMatrixActions<
    TContext,
    TAction extends {
      id: string;
      order?: number;
      handlesActionId?: string;
      isAvailable?: (context: TContext, runtimeContext: CalendarRuntimeContext) => boolean;
    },
  >(
    registry: Map<string, TAction>,
    actionId: string | undefined,
    context: TContext,
    runtimeContext: CalendarRuntimeContext,
  ) {
    if (!actionId) {
      return [];
    }

    return this.sortActions([...registry.values()])
      .filter((action) => action.id === actionId || action.handlesActionId === actionId)
      .filter((action) => action.isAvailable?.(context, runtimeContext) ?? true);
  }

  private sortActions<TAction extends { order?: number }>(actions: TAction[]) {
    return actions.sort((left, right) => (left.order ?? 0) - (right.order ?? 0));
  }
}

export const calendarActionRegistry = new CalendarActionRegistry();
