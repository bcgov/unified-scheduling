import { useFetchAPI } from './useFetchAPI';

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export interface ApiCalendarDataRequest {
  startDate: string;
  endDate: string;
  timeZoneId?: string;
  locationId?: number;
  filters?: Record<string, unknown>;
}

export interface ApiCalendarEventResponse {
  id: number;
  eventSeriesId?: number;
  title: string;
  description?: string;
  notes?: string;
  color?: string;
  startAtUtc: string;
  endAtUtc?: string;
  seriesStartAtUtc?: string;
  seriesEndAtUtc?: string;
  timeZoneId?: string;
  allDay: boolean;
  isException: boolean;
  eventTypeCode: string;
  statusTypeCode: string;
  cancelledAt?: string;
  cancelledByUserId?: string;
  cancellationReason?: string;
  sourceModule: string;
  locationId?: number;
}

export interface ApiCalendarDataResponse {
  moduleId: string;
  contributionId: string;
  events: ApiCalendarEventResponse[];
  conflicts: ApiCalendarConflictResponse[];
}

export interface ApiCalendarConflictEventResponse {
  eventId: number;
  eventTypeCode: string;
  sourceModule: string;
  title: string;
  start: string;
  end: string;
}

export interface ApiCalendarConflictResponse {
  id: string;
  entry: ApiCalendarConflictEventResponse;
  overlaps: ApiCalendarConflictEventResponse;
  resourceId: string;
  overlapStart: string;
  overlapEnd: string;
  isOverridden: boolean;
  overrideId?: number;
  overrideNote?: string;
  createdById?: string | null;
  createdOn?: string | null;
  updatedById?: string | null;
  updatedOn?: string | null;
}

export interface ApiAuditFields {
  createdById?: string | null;
  createdOn?: string | null;
  updatedById?: string | null;
  updatedOn?: string | null;
}

export interface ApiCalendarConflictOverrideResponse extends ApiAuditFields {
  id: number;
  firstEventId: number;
  secondEventId: number;
  note: string;
}

export interface ApiCalendarConflictOverrideRequest {
  firstEventId: number;
  secondEventId: number;
  note: string;
}

export const postApiCalendarData = async (
  request: ApiCalendarDataRequest,
  options?: FetchOptions,
): Promise<ApiCalendarDataResponse> => {
  const { data, error, execute } = useFetchAPI<ApiCalendarDataResponse>(
    {
      url: `${import.meta.env.BASE_URL}api/calendar/events`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    {
      ...options,
      options: {
        immediate: false,
        ...options?.options,
      },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value || { moduleId: 'calendar', contributionId: 'calendar.events', events: [], conflicts: [] };
};

export const postApiCalendarConflictOverride = async (
  request: ApiCalendarConflictOverrideRequest,
  options?: FetchOptions,
): Promise<ApiCalendarConflictOverrideResponse> => {
  const { data, error, execute } = useFetchAPI<ApiCalendarConflictOverrideResponse>(
    {
      url: `${import.meta.env.BASE_URL}api/calendar/conflicts/overrides`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    { ...options, options: { immediate: false, ...options?.options } },
  );

  await execute();
  if (error.value) throw error.value;
  if (!data.value) throw new Error('The conflict override response was empty.');
  return data.value;
};
