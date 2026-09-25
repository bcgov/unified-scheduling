import type { AssignmentSavePayload } from './calendarSchedulingAssignmentForm';
import * as assignmentApi from './calendarSchedulingAssignmentApi';

export interface SaveAssignmentOptions {
  payload: AssignmentSavePayload;
  isEdit: boolean;
  assignmentEntryId?: number;
  assignmentSeriesId?: number;
}

export function useSchedulingAssignmentMutation() {
  function save(options: SaveAssignmentOptions) {
    const { payload } = options;
    if (options.isEdit) {
      if (payload.kind === 'series') {
        if (!isValidId(options.assignmentSeriesId)) {
          throw new Error('An assignment series ID is required when editing a series.');
        }
        return assignmentApi.updateAssignmentSeries(options.assignmentSeriesId, payload.body);
      }

      if (!isValidId(options.assignmentEntryId)) {
        throw new Error('An assignment entry ID is required when editing an entry.');
      }
      return assignmentApi.updateAssignmentEntry(options.assignmentEntryId, payload.body);
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
