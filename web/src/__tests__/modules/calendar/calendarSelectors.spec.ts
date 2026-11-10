import { describe, expect, it } from 'vitest';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import {
  getCalendarConflictsForEvent,
  getCalendarConflictsForEventAndResource,
  getCalendarRestrictedWarningsForEvent,
} from '@/modules/calendar/calendarSelectors';

const createConflict = (reportOnEntry: boolean, reportOnOverlaps: boolean): CalendarConflict => ({
  id: 'physical-pair',
  title: 'Leave ↔ Assignment',
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
    sourceModule: 'scheduling',
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
  it('matches restricted warnings by source-qualified identity and resource while retaining numeric scheduling IDs', () => {
    const training = {
      sourceModule: 'training',
      eventId: 'training.user-training.42',
      resourceId: 'user-1',
      message: 'Restricted conflict',
    };
    const scheduling = { ...training, sourceModule: 'scheduling', eventId: '42' };
    const warnings = [
      training,
      scheduling,
      { ...training, resourceId: 'user-2' },
      { ...training, sourceModule: 'calendar' },
    ];

    expect(
      getCalendarRestrictedWarningsForEvent(
        { sourceModule: 'training', eventId: training.eventId },
        warnings,
        'user-1',
      ),
    ).toEqual([training]);
    expect(getCalendarRestrictedWarningsForEvent(42, warnings, 'user-1')).toEqual([scheduling]);
    expect(getCalendarRestrictedWarningsForEvent({ sourceModule: 'training', eventId: '42' }, warnings)).toEqual([]);
    expect(getCalendarRestrictedWarningsForEvent(null, warnings)).toEqual([]);
    expect(getCalendarRestrictedWarningsForEvent(undefined, warnings)).toEqual([]);
  });

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

  it('matches source-qualified training identities without cross-module or numeric collisions', () => {
    const conflict = createConflict(true, true);
    conflict.entry = { ...conflict.entry, sourceModule: 'training', eventId: 'training.user-training.42' };
    conflict.overlaps = { ...conflict.overlaps, sourceModule: 'calendar', eventId: '1' };
    expect(
      getCalendarConflictsForEvent({ sourceModule: 'training', eventId: 'training.user-training.42' }, [conflict]),
    ).toEqual([conflict]);
    expect(
      getCalendarConflictsForEvent({ sourceModule: 'calendar', eventId: 'training.user-training.42' }, [conflict]),
    ).toEqual([]);
    expect(getCalendarConflictsForEvent(42, [conflict])).toEqual([]);
    expect(getCalendarConflictsForEvent(1, [conflict])).toEqual([]);
  });
});
