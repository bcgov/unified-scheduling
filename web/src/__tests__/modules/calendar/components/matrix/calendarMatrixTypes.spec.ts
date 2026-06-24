import { describe, expect, it } from 'vitest';
import { calendarMatrixContextKey } from '@/modules/calendar/components/matrix/calendarMatrixContext';
import {
  resolveMatrixStatusClass,
  sanitizeMatrixClassToken,
  toRgba,
} from '@/modules/calendar/components/matrix/calendarMatrixDisplayUtils';
import { CalendarMatrixActionType, CalendarMatrixEventGroupVariant } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

describe('calendarMatrixTypes', () => {
  it('exports the supported matrix action types', () => {
    expect(CalendarMatrixActionType).toEqual({
      Button: 'button',
      Custom: 'custom',
      Menu: 'menu',
    });
  });

  it('exports the supported matrix event group variants', () => {
    expect(CalendarMatrixEventGroupVariant).toEqual({
      Primary: 'primary',
      Secondary: 'secondary',
      Muted: 'muted',
      Warning: 'warning',
    });
  });
});

describe('calendarMatrixContext', () => {
  it('uses a stable injection symbol', () => {
    expect(typeof calendarMatrixContextKey).toBe('symbol');
    expect(String(calendarMatrixContextKey)).toContain('calendarMatrixContext');
  });
});

describe('calendarMatrixDisplayUtils', () => {
  it('sanitizes class tokens with fallbacks', () => {
    expect(sanitizeMatrixClassToken(' Draft item ', 'active')).toBe('draft-item');
    expect(sanitizeMatrixClassToken(undefined, 'active')).toBe('active');
    expect(sanitizeMatrixClassToken(' ', 'active')).toBe('active');
  });

  it('resolves status classes', () => {
    expect(resolveMatrixStatusClass('Draft')).toBe('draft');
    expect(resolveMatrixStatusClass('cancelled-event')).toBe('cancelled');
    expect(resolveMatrixStatusClass(undefined)).toBe('active');
  });

  it('converts hex colours to rgba and clamps alpha', () => {
    expect(toRgba('#369', 0.5)).toBe('rgba(51, 102, 153, 0.5)');
    expect(toRgba('#336699', 2)).toBe('rgba(51, 102, 153, 1)');
    expect(toRgba('#336699', -1)).toBe('rgba(51, 102, 153, 0)');
    expect(toRgba('not-a-colour', 0.5)).toBeUndefined();
  });
});
