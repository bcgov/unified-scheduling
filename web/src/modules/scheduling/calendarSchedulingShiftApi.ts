import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import {
  deleteApiSchedulingShiftEntriesId,
  deleteApiSchedulingShiftSeriesId,
  getApiSchedulingShiftSeriesId,
  postApiSchedulingShiftEntries,
  postApiSchedulingShiftEntriesIdExpire,
  postApiSchedulingShiftEntriesIdPublish,
  postApiSchedulingShiftSeries,
  postApiSchedulingShiftSeriesIdExpire,
  postApiSchedulingShiftSeriesIdPublish,
  putApiSchedulingShiftEntriesId,
  putApiSchedulingShiftSeriesId,
} from '@/api-access/generated/shift/shift';

export async function createShiftEntry(body: ShiftEntryRequest) {
  const result = postApiSchedulingShiftEntries(body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function createShiftSeries(body: ShiftSeriesRequest) {
  const result = postApiSchedulingShiftSeries(body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function updateShiftEntry(id: number, body: ShiftEntryRequest) {
  const result = putApiSchedulingShiftEntriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function updateShiftSeries(id: number, body: ShiftSeriesRequest) {
  const result = putApiSchedulingShiftSeriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadShiftSeries(id: number) {
  const result = getApiSchedulingShiftSeriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteShiftEntry(id: number) {
  const result = deleteApiSchedulingShiftEntriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteShiftSeries(id: number) {
  const result = deleteApiSchedulingShiftSeriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function publishShiftEntry(id: number) {
  const result = postApiSchedulingShiftEntriesIdPublish(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function publishShiftSeries(id: number) {
  const result = postApiSchedulingShiftSeriesIdPublish(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function cancelShiftEntry(id: number) {
  const result = postApiSchedulingShiftEntriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function cancelShiftSeries(id: number) {
  const result = postApiSchedulingShiftSeriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}
