import { describe, expect, it } from 'vitest';
import { promoteAssignmentShiftEntryLinks } from '@/modules/scheduling/useSchedulingAssignmentShiftOptions';

describe('promoteAssignmentShiftEntryLinks', () => {
  it('promotes selected occurrences to unique series links and preserves selected users', () => {
    expect(
      promoteAssignmentShiftEntryLinks(
        [{ shiftEntryId: 10, assignedUserIds: ['entry-user'] }],
        [],
        [{ id: 10, shiftSeriesId: 20 }],
        [{ id: 20, userIds: ['series-user'] }],
      ),
    ).toEqual([{ shiftSeriesId: 20, assignedUserIds: ['series-user'] }]);
  });

  it('does not duplicate an existing series selection', () => {
    const existing = [{ shiftSeriesId: 20, assignedUserIds: ['selected-user'] }];
    expect(
      promoteAssignmentShiftEntryLinks(
        [{ shiftEntryId: 10, assignedUserIds: ['entry-user'] }],
        existing,
        [{ id: 10, shiftSeriesId: 20 }],
        [{ id: 20, userIds: ['series-user'] }],
      ),
    ).toEqual(existing);
  });
});
