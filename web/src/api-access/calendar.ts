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

  return data.value || { moduleId: 'calendar', contributionId: 'calendar.events', events: [] };
};
