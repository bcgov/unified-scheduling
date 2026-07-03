import type {
  CalendarMatrixCell,
  CalendarMatrixCellHeader,
  CalendarMatrixDragPayload,
  CalendarMatrixCellDropContext,
  CalendarMatrixResource,
  CalendarMatrixSidePanel,
  CalendarMatrixViewModel,
  CalendarMatrixActionType,
} from '../components/matrix/calendarMatrixTypes';
import type { CalendarQueryContext, CalendarRuntimeContext, CalendarEventBase } from '../calendarTypes';

export interface CalendarCreateContext {
  startDate: string;
  endDate: string;
  activeViewId?: string;
  locationId?: number;
  filters: Record<string, unknown>;
}

export interface CalendarCreateAction {
  id: string;
  label: string;
  moduleId: string;
  disabled?: boolean;
  run?: (context: CalendarCreateContext) => void | Promise<void>;
  isAvailable?: (createContext: CalendarCreateContext, runtimeContext: CalendarRuntimeContext) => boolean;
}

export interface CalendarToolbarAction {
  id: string;
  label: string;
  disabled?: boolean;
  variant?: 'text' | 'outlined' | 'flat';
  onClick?: () => void | Promise<void>;
}

export interface CalendarViewDetailActionContext {
  event: CalendarEventBase;
  viewId: string;
  queryContext: CalendarQueryContext;
  runtimeContext: CalendarRuntimeContext;
}

export interface CalendarViewDetailAction {
  id: string;
  moduleId: string;
  isAvailable?: (context: CalendarViewDetailActionContext) => boolean;
  run: (context: CalendarViewDetailActionContext) => void | Promise<void>;
}

interface CalendarMatrixActionBase {
  id: string;
  moduleId: string;
  label?: string;
  order?: number;
  handlesActionId?: string;
}

export interface CalendarMatrixSidePanelActionContext {
  panel: CalendarMatrixSidePanel;
  model: CalendarMatrixViewModel;
  actionId?: string;
}

export interface CalendarMatrixSidePanelAction extends CalendarMatrixActionBase {
  isAvailable?: (context: CalendarMatrixSidePanelActionContext, runtimeContext: CalendarRuntimeContext) => boolean;
  execute: (
    context: CalendarMatrixSidePanelActionContext,
    runtimeContext: CalendarRuntimeContext,
  ) => void | Promise<void>;
}

export interface CalendarMatrixResourceActionContext {
  resource: CalendarMatrixResource;
  model: CalendarMatrixViewModel;
  cell?: CalendarMatrixCell;
  actionId?: string;
}

export interface CalendarMatrixResourceAction extends CalendarMatrixActionBase {
  isAvailable?: (context: CalendarMatrixResourceActionContext, runtimeContext: CalendarRuntimeContext) => boolean;
  execute: (
    context: CalendarMatrixResourceActionContext,
    runtimeContext: CalendarRuntimeContext,
  ) => void | Promise<void>;
}

export interface CalendarMatrixCellHeaderActionContext {
  cell: CalendarMatrixCell;
  header: CalendarMatrixCellHeader;
  model: CalendarMatrixViewModel;
  actionId?: string;
  actionType?: CalendarMatrixActionType;
}

export interface CalendarMatrixCellHeaderAction extends CalendarMatrixActionBase {
  isAvailable?: (context: CalendarMatrixCellHeaderActionContext, runtimeContext: CalendarRuntimeContext) => boolean;
  execute: (
    context: CalendarMatrixCellHeaderActionContext,
    runtimeContext: CalendarRuntimeContext,
  ) => void | Promise<void>;
}

export interface CalendarMatrixEventBlockActionContext {
  event: CalendarEventBase;
  model: CalendarMatrixViewModel;
  actionId?: string;
  actionType?: CalendarMatrixActionType;
}

export interface CalendarMatrixEventBlockAction extends CalendarMatrixActionBase {
  isAvailable?: (context: CalendarMatrixEventBlockActionContext, runtimeContext: CalendarRuntimeContext) => boolean;
  execute: (
    context: CalendarMatrixEventBlockActionContext,
    runtimeContext: CalendarRuntimeContext,
  ) => void | Promise<void>;
}

export interface CalendarDropAction extends CalendarMatrixActionBase {
  isAvailable?: (
    drag: CalendarMatrixDragPayload,
    drop: CalendarMatrixCellDropContext,
    runtimeContext: CalendarRuntimeContext,
  ) => boolean;
  execute: (
    drag: CalendarMatrixDragPayload,
    drop: CalendarMatrixCellDropContext,
    runtimeContext: CalendarRuntimeContext,
  ) => void | Promise<void>;
}
