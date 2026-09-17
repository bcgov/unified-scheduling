import { expandSchedulingRecurrence } from '@/modules/scheduling/schedulingRecurrence';
import { DateTime } from 'luxon';
import { describe, expect, it } from 'vitest';

describe('schedulingRecurrence', () => {
  it('preserves local wall-clock time across the spring DST transition', () => {
    const first = DateTime.fromISO('2026-03-01T09:00', { zone: 'America/Vancouver' });
    const occurrences = expandSchedulingRecurrence(first, first.plus({ hours: 8 }).diff(first), 'FREQ=WEEKLY;COUNT=3');

    expect(occurrences.map((occurrence) => occurrence.start.toFormat("yyyy-MM-dd'T'HH:mmZZ"))).toEqual([
      '2026-03-01T09:00-08:00',
      '2026-03-08T09:00-07:00',
      '2026-03-15T09:00-07:00',
    ]);
  });

  it('supports positional monthly rules through rrule', () => {
    const first = DateTime.fromISO('2026-01-12T09:00', { zone: 'America/Vancouver' });
    const occurrences = expandSchedulingRecurrence(
      first,
      first.plus({ hours: 1 }).diff(first),
      'RRULE:FREQ=MONTHLY;BYDAY=MO;BYSETPOS=2;COUNT=3',
    );

    expect(occurrences.map((occurrence) => occurrence.dateKey)).toEqual(['2026-01-12', '2026-02-09', '2026-03-09']);
  });
});
