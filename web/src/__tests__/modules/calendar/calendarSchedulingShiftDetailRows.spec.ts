import { describe, expect, it } from 'vitest';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { createShiftDetailRows } from '@/modules/scheduling/calendarSchedulingShiftDetailRows';

const event: CalendarEventBase = {
  id: '1',
  type: 'shift',
  sourceModule: 'scheduling',
  title: 'Morning shift',
  start: '2026-06-29T16:00:00Z',
  end: '2026-06-30T00:00:00Z',
  timeZoneId: 'America/Vancouver',
  notes: '  Event note  ',
  resourceIds: ['user-1'],
};

describe('createShiftDetailRows', () => {
  it('formats entry details with resolved assignee labels', () => {
    const rows = createShiftDetailRows({
      event,
      series: null,
      timeZoneId: 'America/Vancouver',
      employeeOptions: [{ code: 'user-1', description: 'Avery Chen' }],
    });

    expect(rows).toContainEqual({ label: 'Assignee(s)', value: 'Avery Chen' });
    expect(rows).toContainEqual({ label: 'Date', value: 'June 29, 2026' });
    expect(rows).toContainEqual({ label: 'Notes', value: 'Event note' });
    expect(rows).toHaveLength(4);
  });

  it('adds recurrence display data for series details', () => {
    const rows = createShiftDetailRows({
      event,
      series: {
        userIds: ['user-2'],
        startAtUtc: '2026-06-30T16:00:00Z',
        endAtUtc: '2026-07-01T00:00:00Z',
        notes: null,
        recurrenceRule: 'FREQ=WEEKLY',
      },
      timeZoneId: 'America/Vancouver',
      employeeOptions: [],
    });

    expect(rows).toContainEqual({ label: 'Assignee(s)', value: 'user-2' });
    expect(rows).toContainEqual({ label: 'Date', value: 'June 30, 2026' });
    expect(rows).toContainEqual({ label: 'Notes', value: 'None' });
    expect(rows).toContainEqual({
      label: 'Repeat',
      value: '',
      recurrenceRule: 'FREQ=WEEKLY',
      recurrenceStartDate: '2026-06-30T16:00:00Z',
    });
    expect(rows).toHaveLength(5);
  });
});
