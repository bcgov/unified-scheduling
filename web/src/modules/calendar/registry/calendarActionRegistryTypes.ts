import type { CalendarQueryContext, CalendarRuntimeContext, CalendarEventBase } from '../calendarTypes';

export interface CalendarCreateContext {
  startDate: string;
  endDate: string;
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
