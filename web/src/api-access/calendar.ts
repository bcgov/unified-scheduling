import { useFetchAPI } from './useFetchAPI';

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export interface ApiCalendarEventsRequest {
  startDate: string;
  endDate: string;
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
  status?: string;
  locationId?: number;
}

export const postApiCalendarEvents = async (
  request: ApiCalendarEventsRequest,
  options?: FetchOptions,
): Promise<ApiCalendarEventResponse[]> => {
  const { data, error, execute } = useFetchAPI<ApiCalendarEventResponse[]>(
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

  return data.value || [];
};
