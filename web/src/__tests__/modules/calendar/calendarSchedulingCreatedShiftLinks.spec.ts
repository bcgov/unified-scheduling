import { beforeEach, describe, expect, it, vi } from 'vitest';

const syncAssignmentEntryLinks = vi.fn();
const syncAssignmentSeriesLinks = vi.fn();

vi.mock('@/modules/scheduling/calendarSchedulingShiftAssignmentApi', () => ({
  syncAssignmentEntryLinks,
  syncAssignmentSeriesLinks,
}));

describe('syncCreatedShiftAssignmentLinks', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('links a created shift entry to selected assignment entries', async () => {
    const { syncCreatedShiftAssignmentLinks } =
      await import('@/modules/scheduling/calendarSchedulingCreatedShiftLinks');

    await syncCreatedShiftAssignmentLinks('entry', 321, {
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
      assignmentEntryLinks: [
        {
          assignmentEntryId: 251,
          assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
        },
      ],
    });

    expect(syncAssignmentEntryLinks).toHaveBeenCalledWith(
      251,
      [
        {
          shiftEntryId: 321,
          assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
        },
      ],
      [],
    );
    expect(syncAssignmentSeriesLinks).not.toHaveBeenCalled();
  });

  it('links a created shift series to selected assignment series', async () => {
    const { syncCreatedShiftAssignmentLinks } =
      await import('@/modules/scheduling/calendarSchedulingCreatedShiftLinks');

    await syncCreatedShiftAssignmentLinks('series', 322, {
      repeatMode: 'custom',
      publish: 'no',
      cancel: 'no',
      assignmentSeriesLinks: [
        {
          assignmentSeriesId: 252,
          assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
        },
      ],
    });

    expect(syncAssignmentSeriesLinks).toHaveBeenCalledWith(
      252,
      [
        {
          shiftSeriesId: 322,
          assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
        },
      ],
      [],
    );
    expect(syncAssignmentEntryLinks).not.toHaveBeenCalled();
  });
});
