import type { AssignmentEntryRequest } from '@/api-access/generated/models/assignmentEntryRequest';
import type { AssignmentSeriesRequest } from '@/api-access/generated/models/assignmentSeriesRequest';
import type { GetApiSchedulingAssignmentsEntriesParams } from '@/api-access/generated/models/getApiSchedulingAssignmentsEntriesParams';
import type { GetApiSchedulingAssignmentsSeriesParams } from '@/api-access/generated/models/getApiSchedulingAssignmentsSeriesParams';
import {
  getApiSchedulingAssignmentsEntriesId,
  getApiSchedulingAssignmentsSeriesId,
  getApiSchedulingAssignmentsEntries,
  getApiSchedulingAssignmentsSeries,
  postApiSchedulingAssignmentsEntriesIdExpire,
  postApiSchedulingAssignmentsSeriesIdExpire,
  postApiSchedulingAssignmentsEntries,
  postApiSchedulingAssignmentsSeries,
  putApiSchedulingAssignmentsEntriesId,
  putApiSchedulingAssignmentsSeriesId,
} from '@/api-access/generated/assignment/assignment';

export async function createAssignmentEntry(body: AssignmentEntryRequest) {
  const result = postApiSchedulingAssignmentsEntries(body, { options: { immediate: false } });
  await result.execute();
  return {
    data: result.data,
    error: result.error,
    execute: result.execute,
  };
}

export async function updateAssignmentEntry(id: number, body: AssignmentEntryRequest) {
  const result = putApiSchedulingAssignmentsEntriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function updateAssignmentSeries(id: number, body: AssignmentSeriesRequest) {
  const result = putApiSchedulingAssignmentsSeriesId(id, body, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadAssignmentEntry(id: number) {
  const result = getApiSchedulingAssignmentsEntriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadAssignmentSeriesById(id: number) {
  const result = getApiSchedulingAssignmentsSeriesId(id, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function expireAssignmentEntry(id: number) {
  const result = postApiSchedulingAssignmentsEntriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function expireAssignmentSeries(id: number) {
  const result = postApiSchedulingAssignmentsSeriesIdExpire(id, null, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function createAssignmentSeries(body: AssignmentSeriesRequest) {
  const result = postApiSchedulingAssignmentsSeries(body, { options: { immediate: false } });
  await result.execute();
  return {
    data: result.data,
    error: result.error,
    execute: result.execute,
  };
}

export async function loadAssignmentEntries(params: GetApiSchedulingAssignmentsEntriesParams) {
  const result = getApiSchedulingAssignmentsEntries(params, { options: { immediate: false } });
  await result.execute();
  return result;
}

export async function loadAssignmentSeries(params: GetApiSchedulingAssignmentsSeriesParams) {
  const result = getApiSchedulingAssignmentsSeries(params, { options: { immediate: false } });
  await result.execute();
  return result;
}
