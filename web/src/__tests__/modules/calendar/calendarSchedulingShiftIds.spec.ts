import { describe, expect, it } from 'vitest';
import {
  parsePositiveInteger,
  resolveShiftEntryId,
  resolveShiftSeriesId,
} from '@/modules/scheduling/calendarSchedulingShiftIds';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';

describe('calendarSchedulingShiftIds', () => {
  it.each([
    [42, 42],
    ['42', 42],
    [0, null],
    [-1, null],
    [1.5, null],
    ['', null],
    [true, null],
    [null, null],
  ])('parses %j as %j', (value, expected) => {
    expect(parsePositiveInteger(value)).toBe(expected);
  });

  it('resolves shift IDs from scheduling event metadata', () => {
    const event = {
      id: 'shift-42',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: 'Shift',
      start: '2026-09-02T16:00:00Z',
      metadata: {
        shiftEntryId: '42',
        shiftSeriesId: 7,
      },
    } as CalendarEventBase;

    expect(resolveShiftEntryId(event)).toBe(42);
    expect(resolveShiftSeriesId(event)).toBe(7);
  });
});
