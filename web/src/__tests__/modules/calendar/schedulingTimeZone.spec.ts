import { defaultSchedulingTimeZoneId, resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { describe, expect, it } from 'vitest';

describe('schedulingTimeZone', () => {
  it('uses the first valid Scheduling context timezone', () => {
    expect(resolveSchedulingTimeZoneId('', 'America/Toronto', 'America/Vancouver')).toBe('America/Toronto');
  });

  it('uses the deterministic Scheduling fallback for missing or invalid zones', () => {
    expect(resolveSchedulingTimeZoneId(undefined, 'Not/AZone')).toBe(defaultSchedulingTimeZoneId);
  });
});
