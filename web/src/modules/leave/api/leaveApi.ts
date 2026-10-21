import type {
  SchedulingLeaveEntryRequest as LeaveEntryRequest,
  SchedulingLeaveEntryResponse as LeaveEntryResponse,
  SchedulingLeaveSeriesRequest as LeaveSeriesRequest,
  SchedulingLeaveSeriesResponse as LeaveSeriesResponse,
} from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';

const leaveEntryUrl = (id: number) => `/api/scheduling/leave/entries/${id}`;
const leaveSeriesUrl = (id: number) => `/api/scheduling/leave/series/${id}`;

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
