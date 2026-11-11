import type { AssignmentSavePayload } from './calendarSchedulingAssignmentForm';
import type { CalendarConflictAcknowledgement } from '@/api-access/generated/models/calendarConflictAcknowledgement';
import * as assignmentApi from './calendarSchedulingAssignmentApi';

export interface SaveAssignmentOptions {
  payload: AssignmentSavePayload;
  isEdit: boolean;
  concurrencyToken?: number;
  assignmentEntryId?: number;
  assignmentSeriesId?: number;
  conflictOverrides?: CalendarConflictAcknowledgement[];
}

export function useSchedulingAssignmentMutation() {
  function save(options: SaveAssignmentOptions) {
    const { payload } = options;
    if (options.isEdit) {
      if (!Number.isInteger(options.concurrencyToken) || !options.concurrencyToken || options.concurrencyToken < 0) {
        throw new Error('Reload the assignment before saving. Its version is missing.');
      }
      if (payload.kind === 'series') {
        if (!isValidId(options.assignmentSeriesId)) {
          throw new Error('An assignment series ID is required when editing a series.');
        }
        return assignmentApi.updateAssignmentSeries(options.assignmentSeriesId, {
          ...payload.body,
          concurrencyToken: options.concurrencyToken,
        });
      }

      if (!isValidId(options.assignmentEntryId)) {
        throw new Error('An assignment entry ID is required when editing an entry.');
      }
      return assignmentApi.updateAssignmentEntry(options.assignmentEntryId, {
        ...payload.body,
        concurrencyToken: options.concurrencyToken,
        conflictOverrides: options.conflictOverrides,
      });
    }
    return payload.kind === 'series'
      ? assignmentApi.createAssignmentSeries(payload.body)
      : assignmentApi.createAssignmentEntry(payload.body);
  }

  function remove(kind: 'entry' | 'series', id: number, expire: boolean) {
    if (kind === 'series') {
      return expire ? assignmentApi.expireAssignmentSeries(id) : assignmentApi.deleteAssignmentSeries(id);
    }
    return expire ? assignmentApi.expireAssignmentEntry(id) : assignmentApi.deleteAssignmentEntry(id);
  }

  return { remove, save };
}

function isValidId(value: number | undefined): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0;
}
