import { beforeEach, describe, expect, it, vi } from 'vitest';

const assignmentApi = vi.hoisted(() => ({
  createAssignmentEntry: vi.fn(),
  createAssignmentSeries: vi.fn(),
  updateAssignmentEntry: vi.fn(),
  updateAssignmentSeries: vi.fn(),
  deleteAssignmentEntry: vi.fn(),
  deleteAssignmentSeries: vi.fn(),
  expireAssignmentEntry: vi.fn(),
  expireAssignmentSeries: vi.fn(),
}));

vi.mock('@/modules/scheduling/calendarSchedulingAssignmentApi', () => assignmentApi);

import { useSchedulingAssignmentMutation } from '@/modules/scheduling/useSchedulingAssignmentMutation';

describe('useSchedulingAssignmentMutation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it.each([
    { kind: 'entry' as const, error: 'An assignment entry ID is required when editing an entry.' },
    { kind: 'series' as const, error: 'An assignment series ID is required when editing a series.' },
  ])('does not call create or update when an edited $kind has no ID', ({ kind, error }) => {
    const mutation = useSchedulingAssignmentMutation();

    expect(() => mutation.save({ payload: { kind, body: {} } as never, isEdit: true })).toThrow(error);
    expect(assignmentApi.createAssignmentEntry).not.toHaveBeenCalled();
    expect(assignmentApi.createAssignmentSeries).not.toHaveBeenCalled();
    expect(assignmentApi.updateAssignmentEntry).not.toHaveBeenCalled();
    expect(assignmentApi.updateAssignmentSeries).not.toHaveBeenCalled();
  });

  it.each([
    { kind: 'entry' as const, isEdit: false, expected: assignmentApi.createAssignmentEntry },
    { kind: 'series' as const, isEdit: false, expected: assignmentApi.createAssignmentSeries },
    { kind: 'entry' as const, isEdit: true, expected: assignmentApi.updateAssignmentEntry },
    { kind: 'series' as const, isEdit: true, expected: assignmentApi.updateAssignmentSeries },
  ])('uses the correct endpoint for a valid $kind mutation when isEdit is $isEdit', ({ kind, isEdit, expected }) => {
    const mutation = useSchedulingAssignmentMutation();
    const body = {} as never;

    mutation.save({
      payload: { kind, body } as never,
      isEdit,
      assignmentEntryId: 41,
      assignmentSeriesId: 42,
    });

    expect(expected).toHaveBeenCalledOnce();
    if (isEdit) {
      expect(expected).toHaveBeenCalledWith(kind === 'entry' ? 41 : 42, body);
    } else {
      expect(expected).toHaveBeenCalledWith(body);
    }
  });
});
