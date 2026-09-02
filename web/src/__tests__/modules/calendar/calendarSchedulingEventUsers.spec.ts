import { describe, expect, it } from 'vitest';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { resolveCalendarEventUserIds } from '@/modules/scheduling/calendarSchedulingEventUsers';

const schedulingEvent = {
  id: 'shift-1',
  type: 'scheduling.shift',
  sourceModule: 'scheduling',
  title: 'Shift',
  start: '2026-09-02T16:00:00Z',
  resourceIds: ['resource-user'],
  metadata: {},
} as CalendarEventBase;

describe('resolveCalendarEventUserIds', () => {
  it('prefers scheduling metadata users', () => {
    expect(
      resolveCalendarEventUserIds({
        ...schedulingEvent,
        metadata: { userIds: ['metadata-user'] },
      } as CalendarEventBase),
    ).toEqual(['metadata-user']);
  });

  it('controls the scheduling resource fallback explicitly', () => {
    expect(resolveCalendarEventUserIds(schedulingEvent)).toEqual(['resource-user']);
    expect(resolveCalendarEventUserIds(schedulingEvent, { fallbackToResourceIds: false })).toEqual([]);
  });
});
