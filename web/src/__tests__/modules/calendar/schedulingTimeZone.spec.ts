import { defaultSchedulingTimeZoneId, resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { describe, expect, it } from 'vitest';
import { toDateTime, toOffsetDateTimeString } from '@/utils/date';

describe('schedulingTimeZone', () => {
  it.each(['2026-07-01', '2026-12-01'])('uses the runtime Vancouver UTC-7 rules on %s', (date) => {
    const instant = `${date}T16:30:00Z`;
    const local = toDateTime(instant, 'America/Vancouver');
    expect(local.offset).toBe(-420);
    expect(local.toFormat('HH:mm')).toBe('09:30');
    expect(Date.parse(toOffsetDateTimeString(date, '09:30', 'America/Vancouver'))).toBe(Date.parse(instant));
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: 'America/Vancouver',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23',
    }).formatToParts(new Date(instant));
    expect(parts.find((part) => part.type === 'hour')?.value).toBe('09');
    expect(parts.find((part) => part.type === 'minute')?.value).toBe('30');
  });

  it('uses Edmonton rather than Vancouver rules for local training conversions', () => {
    expect(toDateTime('2026-07-01T16:30:00Z', 'America/Edmonton').toFormat('HH:mm')).toBe('10:30');
    expect(Date.parse(toOffsetDateTimeString('2026-07-01', '10:30', 'America/Edmonton'))).toBe(
      Date.parse('2026-07-01T16:30:00Z'),
    );
  });
  it('uses the first valid Scheduling context timezone', () => {
    expect(resolveSchedulingTimeZoneId('', 'America/Toronto', 'America/Vancouver')).toBe('America/Toronto');
  });

  it('uses the deterministic Scheduling fallback for missing or invalid zones', () => {
    expect(resolveSchedulingTimeZoneId(undefined, 'Not/AZone')).toBe(defaultSchedulingTimeZoneId);
  });
});
