import { describe, expect, it } from 'vitest';
import { buildLoanOutRequest } from '@/modules/loanout/loanOutRequest';

describe('loan-out working-hour defaults', () => {
  it.each(['never', 'custom'] as const)('defaults %s loan-outs to 09:00–17:00 in the selected zone', (repeatMode) => {
    const result = buildLoanOutRequest(
      {
        userIds: ['e56808e2-a11d-4175-9ebf-f080e248be73'],
        loanOutLocationId: 9,
        date: '2026-09-09',
        repeatMode,
        recurrenceRule: repeatMode === 'custom' ? 'FREQ=DAILY;COUNT=2' : null,
      },
      { locationId: 5, timeZoneId: 'America/Vancouver' },
    );

    expect(result.error).toBeUndefined();
    if ('request' in result) {
      expect(result.kind).toBe(repeatMode === 'custom' ? 'series' : 'entry');
      expect(result.request.startAtUtc).toBe('2026-09-09T16:00:00Z');
      expect(result.request.endAtUtc).toBe('2026-09-10T00:00:00Z');
    }
  });

  it('uses the selected timezone instead of hardcoded UTC offsets', () => {
    const result = buildLoanOutRequest(
      {
        userIds: ['e56808e2-a11d-4175-9ebf-f080e248be73'],
        loanOutLocationId: 9,
        date: '2026-11-08',
      },
      { locationId: 5, timeZoneId: 'UTC' },
    );

    expect(result.error).toBeUndefined();
    if ('request' in result) {
      expect(result.request.startAtUtc).toBe('2026-11-08T09:00:00Z');
      expect(result.request.endAtUtc).toBe('2026-11-08T17:00:00Z');
    }
  });
});
