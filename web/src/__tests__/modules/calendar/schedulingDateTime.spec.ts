import { describe, expect, it } from 'vitest';
import {
  buildLocalDateTimeRange,
  normalizeTimeOptionValue,
  parseFormDateTime,
  toFormDateTime,
  toUtcIso,
} from '@/modules/scheduling/schedulingDateTime';

describe('schedulingDateTime', () => {
  it('normalizes backend and display time values', () => {
    expect(normalizeTimeOptionValue('09:00:00')).toBe('09:00');
    expect(normalizeTimeOptionValue('5:00 PM')).toBe('17:00');
  });

  it('converts local form values to UTC', () => {
    expect(toUtcIso('2026-06-29', '09:00', 'America/Vancouver')).toBe('2026-06-29T16:00:00Z');
    expect(toUtcIso('', '09:00', 'America/Vancouver')).toBeNull();
  });

  it('hydrates UTC instants as local form values', () => {
    expect(toFormDateTime('2026-06-29T16:00:00Z', 'America/Vancouver')).toEqual({
      date: '2026-06-29',
      time: '09:00',
    });
    expect(toFormDateTime('not-a-date', 'America/Vancouver')).toEqual({ date: '', time: '09:00' });
    expect(parseFormDateTime('not-a-date', 'America/Vancouver')).toBeNull();
  });

  it('returns only valid increasing local ranges', () => {
    expect(buildLocalDateTimeRange('2026-06-29', '09:00', '17:00', 'America/Vancouver')).toMatchObject({
      start: { isValid: true },
      end: { isValid: true },
    });
    expect(buildLocalDateTimeRange('2026-06-29', '17:00', '09:00', 'America/Vancouver')).toBeNull();
  });
});
