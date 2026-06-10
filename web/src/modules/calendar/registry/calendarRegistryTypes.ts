import type { Component } from 'vue';
import type { CalendarPeriod } from '../calendarStore';
import type {
  CalendarEventBase,
  CalendarContributionData,
  CalendarDataResponse,
  CalendarQueryContext,
  CalendarRuntimeContext,
} from '../calendarTypes';

export interface CalendarContributionLoadOptions {
  signal?: AbortSignal;
}

export interface CalendarModuleContribution {
  moduleId: string;
  contributionId: string;
  load: (context: CalendarQueryContext, options?: CalendarContributionLoadOptions) => Promise<CalendarContributionData>;
  isAvailable?: (runtimeContext: CalendarRuntimeContext, queryContext: CalendarQueryContext) => boolean;
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

export interface CalendarViewDefinition<TModel = unknown> {
  id: string;
  label: string;
  order?: number;
  component: Component;
  buildModel: (
    data: CalendarDataResponse,
    queryContext: CalendarQueryContext,
    runtimeContext: CalendarRuntimeContext,
    period: CalendarPeriod,
  ) => TModel;
  isAvailable?: (runtimeContext: CalendarRuntimeContext) => boolean;
  toolbarActions?: CalendarToolbarAction[];
}
