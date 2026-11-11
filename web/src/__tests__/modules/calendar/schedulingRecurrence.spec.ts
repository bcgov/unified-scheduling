import { expandSchedulingRecurrence } from '@/modules/scheduling/schedulingRecurrence';
import { DateTime } from 'luxon';
import { describe, expect, it } from 'vitest';

describe('schedulingRecurrence', () => {
  it.each([
    ['America/Vancouver', '2026-07-13T09:00', '20260714T160000Z', 2],
    ['America/Vancouver', '2026-07-13T09:00', '20260714T155959Z', 1],
    ['Asia/Tokyo', '2026-07-13T09:00', '20260714T000000Z', 2],
    ['Asia/Tokyo', '2026-07-13T09:00', '20260713T235959Z', 1],
    ['America/New_York', '2026-03-07T09:00', '20260308T130000Z', 2],
    ['America/New_York', '2026-03-07T09:00', '20260308T125959Z', 1],
  ] as const)('applies UTC UNTIL inclusively in %s (%s through %s)', (zone, date, until, count) => {
    const first = DateTime.fromISO(date, { zone });
    const occurrences = expandSchedulingRecurrence(
      first,
      first.plus({ hours: 1 }).diff(first),
      `FREQ=DAILY;UNTIL=${until}`,
    );

    expect(occurrences.map((occurrence) => occurrence.start.toISO())).toEqual(
      Array.from({ length: count }, (_, index) => first.plus({ days: index }).toISO()),
    );
  });

  it.each([
    ['FREQ=DAILY;COUNT=3;UNTIL=20280101T000000Z', 3],
    ['FREQ=DAILY;UNTIL=20280101T000000Z', 400],
  ] as const)('preserves the occurrence limit for %s', (rule, count) => {
    const first = DateTime.fromISO('2026-01-01T09:00', { zone: 'Asia/Tokyo' });
    expect(expandSchedulingRecurrence(first, first.plus({ hours: 1 }).diff(first), rule)).toHaveLength(count);
  });

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
