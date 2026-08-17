import { useFetchAPI } from './useFetchAPI';
import { CalendarEventType, type CalendarDataResponse, type CalendarEventResponse } from './generated/models';

export const calendarEventTypes = {
  calendarEvent: CalendarEventType.calendarevent,
} as const;

export type ApiCalendarEventResponse = CalendarEventResponse;

type FetchOptions = Parameters<typeof useFetchAPI>[1];

export interface ApiCalendarEventsRequest {
  startDate: string;
  endDate: string;
  timeZoneId?: string;
  locationId?: number;
  filters?: Record<string, unknown>;
}

export const postApiCalendarEvents = async (
  request: ApiCalendarEventsRequest,
  options?: FetchOptions,
): Promise<CalendarDataResponse> => {
  const { data, error, execute } = useFetchAPI<CalendarDataResponse>(
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

  return data.value || { events: [] };
};
