import { describe, expect, it } from 'vitest';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import {
  getCalendarConflictsForEvent,
  getCalendarConflictsForEventAndResource,
} from '@/modules/calendar/calendarSelectors';

const createConflict = (reportOnEntry: boolean, reportOnOverlaps: boolean): CalendarConflict => ({
  id: 'physical-pair',
  entry: {
    eventId: '1',
    sourceModule: 'scheduling',
    title: 'Assignment',
    start: '2026-07-01T16:00:00Z',
    end: '2026-07-01T18:00:00Z',
    sourceEntityId: 1,
    timeZoneId: 'America/Vancouver',
  },
  overlaps: {
    eventId: '2',
    sourceModule: 'leave',
    title: 'Leave',
    start: '2026-07-01T17:00:00Z',
    end: '2026-07-01T19:00:00Z',
    sourceEntityId: 2,
    timeZoneId: 'America/Vancouver',
  },
  resourceId: 'employee-1',
  overlapStart: '2026-07-01T17:00:00Z',
  overlapEnd: '2026-07-01T18:00:00Z',
  isOverridden: false,
  overrideNote: null,
  createdById: null,
  createdOn: null,
  updatedById: null,
  updatedOn: null,
  reportOnEntry,
  reportOnOverlaps,
});

describe('calendar conflict selectors', () => {
  it.each([
    [true, false],
    [false, true],
    [true, true],
    [false, false],
  ])('honours both reporting flags (%s, %s)', (entry, overlaps) => {
    const conflict = createConflict(entry, overlaps);

    expect(getCalendarConflictsForEvent(1, [conflict])).toEqual(entry ? [conflict] : []);
    expect(getCalendarConflictsForEvent(2, [conflict])).toEqual(overlaps ? [conflict] : []);
    expect(getCalendarConflictsForEvent(3, [conflict])).toEqual([]);
  });

  it('preserves resource filtering and returns one copy of a bidirectional pair', () => {
    const conflict = createConflict(true, true);
    const otherEmployee = { ...conflict, resourceId: 'employee-2' };

    expect(getCalendarConflictsForEventAndResource(1, 'employee-1', [conflict, otherEmployee])).toEqual([conflict]);
    expect(getCalendarConflictsForEventAndResource(2, 'employee-2', [conflict, otherEmployee])).toEqual([
      otherEmployee,
    ]);
    expect(getCalendarConflictsForEvent(null, [conflict])).toEqual([]);
    expect(getCalendarConflictsForEvent(undefined, [conflict])).toEqual([]);
  });
});
