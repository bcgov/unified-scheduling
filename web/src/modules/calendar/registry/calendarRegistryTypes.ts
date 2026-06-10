import type { Component } from 'vue';
import type { CalendarPeriod } from '../calendarStore';
import type {
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
}
