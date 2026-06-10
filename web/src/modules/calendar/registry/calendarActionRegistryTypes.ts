import type { CalendarRuntimeContext } from '../calendarTypes';

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
