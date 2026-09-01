import type { FeatureFlags, Permissions } from '@/api-access/generated/models';
import type { ApiAuditFields } from '@/api-access/calendar';

export interface CalendarEventBase {
  id: string;
  type: string;
  sourceModule: string;
  title: string;
  description?: string;
  notes?: string;
  color?: string;
  eventSeriesId?: number;
  start: string;
  end?: string;
  seriesStartAtUtc?: string;
  seriesEndAtUtc?: string;
  allDay?: boolean;
  isException?: boolean;
  eventTypeCode?: string;
  statusTypeCode?: string;
  cancelledAt?: string;
  cancelledByUserId?: string;
  cancellationReason?: string;
  timeZoneId?: string;
  locationId?: number;
  resourceIds?: string[];
}

export interface CalendarResourceBase {
  id: string;
  type: string;
  sourceModule: string;
  label: string;
}

export interface CalendarConflictEvent {
  eventId: number | null;
  eventTypeCode: string;
  sourceModule: string;
  title: string;
  start: string;
  end: string;
  sourceEntityId?: number | null;
  timeZoneId?: string | null;
}

export interface CalendarConflict extends ApiAuditFields {
  id: string;
  entry: CalendarConflictEvent;
  overlaps: CalendarConflictEvent;
  resourceId: string;
  overlapStart: string;
  overlapEnd: string;
  isOverridden: boolean;
  overrideId?: number;
  overrideNote?: string;
}

export interface CalendarQueryContext {
  startDate: string;
  endDate: string;
  locationId?: number;
  filters: Record<string, unknown>;
}

export interface CalendarRuntimeContext {
  featureFlags: Partial<FeatureFlags>;
  permissions?: readonly Permissions[];
}

export interface CalendarContributionData<
  TEvent extends CalendarEventBase = CalendarEventBase,
  TResource extends CalendarResourceBase = CalendarResourceBase,
  TData = unknown,
> {
  moduleId: string;
  contributionId: string;
  events: TEvent[];
  resources?: TResource[];
  data?: TData;
}

export interface CalendarDataResponse {
  contributions: Record<string, CalendarContributionData>;
}

export interface CalendarFullCalendarViewModel {
  view: 'timeGridDay' | 'timeGridWeek' | 'dayGridMonth' | 'listWeek';
  initialDate?: string;
  events: CalendarEventBase[];
  weekends?: boolean;
}
