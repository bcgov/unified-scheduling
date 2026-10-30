import type {
  SchedulingLeaveEntryRequest as LeaveEntryRequest,
  SchedulingLeaveEntryResponse as LeaveEntryResponse,
  SchedulingLeaveSeriesRequest as LeaveSeriesRequest,
  SchedulingLeaveSeriesResponse as LeaveSeriesResponse,
  SchedulingCalendarEventResponse,
} from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';

const leaveEntryUrl = (id: number) => `/api/scheduling/leave/entries/${id}`;
const leaveSeriesUrl = (id: number) => `/api/scheduling/leave/series/${id}`;
// Calendar fields are returned by the Leave module's own mapper.
export type LeaveCalendarEvent = Omit<SchedulingCalendarEventResponse, 'resourceIds'>;
export async function getLeaveCalendarEvents(
  request: { startAtUtc: string; endAtUtc: string; userIds: string[] },
  signal?: AbortSignal,
) {
  const result = useFetchAPI<LeaveCalendarEvent[]>(
    {
      url: '/api/leave/calendar/events',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: request,
    },
    {
      options: {
        immediate: false,
        beforeFetch({ options }) {
          if (signal) options.signal = options.signal ? AbortSignal.any([options.signal, signal]) : signal;
          return { options };
        },
      },
    },
  );
  await result.execute();
  if (signal?.aborted) throw new DOMException('Leave request was aborted.', 'AbortError');
  if (result.error.value) throw result.error.value;
  if (!result.data.value) throw new Error('Failed to load leave events.');
  return result.data.value;
}

// Read: Leave Entry and Series API functions

export function useUserLeaveEntries(userId: string) {
  return useFetchAPI<LeaveEntryResponse[]>(
    { url: `/api/scheduling/leave/users/${userId}/entries`, method: 'GET' },
    { options: { immediate: false } },
  );
}

export function useUserLeaveSeries(userId: string) {
  return useFetchAPI<LeaveSeriesResponse[]>(
    { url: `/api/scheduling/leave/users/${userId}/series`, method: 'GET' },
    { options: { immediate: false } },
  );
}

export async function getLeaveEntry(id: number) {
  const result = useFetchAPI<LeaveEntryResponse>(
    { url: leaveEntryUrl(id), method: 'GET' },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function getLeaveSeries(id: number) {
  const result = useFetchAPI<LeaveSeriesResponse>(
    { url: leaveSeriesUrl(id), method: 'GET' },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Create: Leave Entry and Series API functions
export async function createLeaveEntry(body: LeaveEntryRequest) {
  const result = useFetchAPI<LeaveEntryResponse>(
    {
      url: '/api/scheduling/leave/entries',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function createLeaveSeries(body: LeaveSeriesRequest) {
  const result = useFetchAPI<LeaveSeriesResponse>(
    {
      url: '/api/scheduling/leave/series',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Update: Leave Entry and Series API functions
export async function updateLeaveEntry(id: number, body: LeaveEntryRequest) {
  const result = useFetchAPI<LeaveEntryResponse>(
    { url: leaveEntryUrl(id), method: 'PUT', headers: { 'Content-Type': 'application/json' }, data: body },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function updateLeaveSeries(id: number, body: LeaveSeriesRequest) {
  const result = useFetchAPI<LeaveSeriesResponse>(
    {
      url: leaveSeriesUrl(id),
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Delete: Leave Entry and Series API functions
export async function deleteLeaveEntry(id: number) {
  const result = useFetchAPI<void>({ url: leaveEntryUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteLeaveSeries(id: number) {
  const result = useFetchAPI<void>({ url: leaveSeriesUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}
