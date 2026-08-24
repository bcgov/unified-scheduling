import { describe, expect, it } from 'vitest';
import { resolveShiftSeriesLinksFromAssignmentSeries } from '@/modules/scheduling/calendarSchedulingAssignmentSeriesLinks';

describe('calendarSchedulingAssignmentSeriesLinks', () => {
  it('hydrates shift series links from an assignment series response', () => {
    expect(
      resolveShiftSeriesLinksFromAssignmentSeries({
        id: 211,
        shiftSeriesLinks: [
          {
            id: 301,
            shiftSeriesId: 201,
            assignmentSeriesId: 211,
            assignedUserIds: ['ff47b192-cee6-4c7b-b069-6c861cf30367'],
          },
        ],
      }),
    ).toEqual([
      {
        shiftSeriesId: 201,
        assignedUserIds: ['ff47b192-cee6-4c7b-b069-6c861cf30367'],
      },
    ]);
  });
});
