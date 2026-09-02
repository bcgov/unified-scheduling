import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import {
  deleteApiSchedulingShiftsEntriesId,
  deleteApiSchedulingShiftsSeriesId,
  getApiSchedulingShiftsEntries,
  getApiSchedulingShiftsEntriesId,
  getApiSchedulingShiftsSeries,
  getApiSchedulingShiftsSeriesId,
  postApiSchedulingShiftsEntries,
  postApiSchedulingShiftsEntriesIdExpire,
  postApiSchedulingShiftsEntriesIdPublish,
  postApiSchedulingShiftsSeries,
  postApiSchedulingShiftsSeriesIdExpire,
  postApiSchedulingShiftsSeriesIdPublish,
  putApiSchedulingShiftsEntriesId,
  putApiSchedulingShiftsSeriesId,
} from '@/api-access/generated/shift/shift';

export async function loadShiftEntries(params?: Parameters<typeof getApiSchedulingShiftsEntries>[0]) {
  const result = getApiSchedulingShiftsEntries(params, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadShiftSeriesList(params?: Parameters<typeof getApiSchedulingShiftsSeries>[0]) {
  const result = getApiSchedulingShiftsSeries(params, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function createShiftEntry(body: ShiftEntryRequest) {
  const result = postApiSchedulingShiftsEntries(body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function createShiftSeries(body: ShiftSeriesRequest) {
  const result = postApiSchedulingShiftsSeries(body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function updateShiftEntry(id: number, body: ShiftEntryRequest) {
  const result = putApiSchedulingShiftsEntriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function updateShiftSeries(id: number, body: ShiftSeriesRequest) {
  const result = putApiSchedulingShiftsSeriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadShiftSeries(id: number) {
  const result = getApiSchedulingShiftsSeriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadShiftEntry(id: number) {
  const result = getApiSchedulingShiftsEntriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteShiftEntry(id: number) {
  const result = deleteApiSchedulingShiftsEntriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function deleteShiftSeries(id: number) {
  const result = deleteApiSchedulingShiftsSeriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function publishShiftEntry(id: number) {
  const result = postApiSchedulingShiftsEntriesIdPublish(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function publishShiftSeries(id: number) {
  const result = postApiSchedulingShiftsSeriesIdPublish(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function cancelShiftEntry(id: number) {
  const result = postApiSchedulingShiftsEntriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function cancelShiftSeries(id: number) {
  const result = postApiSchedulingShiftsSeriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}
