import type {
  SchedulingTimeOffEntryRequest as TimeOffEntryRequest,
  SchedulingTimeOffEntryResponse as TimeOffEntryResponse,
  SchedulingTimeOffSeriesRequest as TimeOffSeriesRequest,
  SchedulingTimeOffSeriesResponse as TimeOffSeriesResponse,
} from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';

const timeOffEntryUrl = (id: number) => `/api/scheduling/timeoff/entries/${id}`;
const timeOffSeriesUrl = (id: number) => `/api/scheduling/timeoff/series/${id}`;

// Read: Time Off Entry and Series API functions

export function useUserTimeOffEntries(userId: string) {
  return useFetchAPI<TimeOffEntryResponse[]>(
    { url: `/api/scheduling/timeoff/users/${userId}/entries`, method: 'GET' },
    { options: { immediate: false } },
  );
}

export function useUserTimeOffSeries(userId: string) {
  return useFetchAPI<TimeOffSeriesResponse[]>(
    { url: `/api/scheduling/timeoff/users/${userId}/series`, method: 'GET' },
    { options: { immediate: false } },
  );
}

export async function getTimeOffEntry(id: number) {
  const result = useFetchAPI<TimeOffEntryResponse>(
    { url: timeOffEntryUrl(id), method: 'GET' },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function getTimeOffSeries(id: number) {
  const result = useFetchAPI<TimeOffSeriesResponse>(
    { url: timeOffSeriesUrl(id), method: 'GET' },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Create: Time Off Entry and Series API functions
export async function createTimeOffEntry(body: TimeOffEntryRequest) {
  const result = useFetchAPI<TimeOffEntryResponse>(
    {
      url: '/api/scheduling/timeoff/entries',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function createTimeOffSeries(body: TimeOffSeriesRequest) {
  const result = useFetchAPI<TimeOffSeriesResponse>(
    {
      url: '/api/scheduling/timeoff/series',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Update: Time Off Entry and Series API functions
export async function updateTimeOffEntry(id: number, body: TimeOffEntryRequest) {
  const result = useFetchAPI<TimeOffEntryResponse>(
    { url: timeOffEntryUrl(id), method: 'PUT', headers: { 'Content-Type': 'application/json' }, data: body },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

export async function updateTimeOffSeries(id: number, body: TimeOffSeriesRequest) {
  const result = useFetchAPI<TimeOffSeriesResponse>(
    {
      url: timeOffSeriesUrl(id),
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    },
    { options: { immediate: false } },
  );
  await result.execute();
  return result;
}

// Delete: Time Off Entry and Series API functions
export async function deleteTimeOffEntry(id: number) {
  const result = useFetchAPI<void>({ url: timeOffEntryUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteTimeOffSeries(id: number) {
  const result = useFetchAPI<void>({ url: timeOffSeriesUrl(id), method: 'DELETE' }, { options: { immediate: false } });
  await result.execute();
  return result;
}
