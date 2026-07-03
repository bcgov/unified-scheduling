import { describe, expect, it } from 'vitest';
import {
  buildCreateShiftPayload,
  buildUpdateShiftPayload,
  createInitialShiftFormDataForCreateAction,
  validateShiftFormData,
  type ShiftResourceFormData,
} from '@/modules/scheduling/calendarSchedulingShiftForm';

const baseFormData: ShiftResourceFormData = {
  ...createInitialShiftFormDataForCreateAction(1),
  title: 'System System shift',
  date: '2026-06-29',
  startTime: '09:00',
  endTime: '17:00',
  statusTypeCode: 'Draft',
  userIds: ['00000000-0000-0000-0000-000000000001'],
};

describe('calendarSchedulingShiftForm', () => {
  it('validates and normalizes shared shift form data', () => {
    const result = validateShiftFormData(
      {
        ...baseFormData,
        startTime: '9:00 AM',
        endTime: '5:00 PM',
      },
      { timeZoneId: 'America/Vancouver' },
    );

    expect(result.data).toMatchObject({
      startTime: '09:00',
      endTime: '17:00',
      cancel: 'no',
    });
    expect(result.errors).toEqual({});
  });

  it('builds create payloads without status mutation fields', () => {
    const payload = buildCreateShiftPayload({
      formData: baseFormData,
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.publish).toBe(false);
    expect(payload?.body).not.toHaveProperty('statusTypeCode');
    expect(payload?.body).not.toHaveProperty('cancelledAt');
    expect(payload?.body).not.toHaveProperty('cancelledByUserId');
    expect(payload?.body).not.toHaveProperty('cancellationReason');
  });

  it('keeps lifecycle intent separate from update payloads', () => {
    const payload = buildUpdateShiftPayload({
      formData: {
        ...baseFormData,
        statusTypeCode: 'Active',
        cancel: 'yes',
      },
      scope: 'entry',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.cancel).toBe(true);
    expect(payload?.body).not.toHaveProperty('statusTypeCode');
    expect(payload?.body).not.toHaveProperty('cancelledAt');
    expect(payload?.body).not.toHaveProperty('cancelledByUserId');
    expect(payload?.body).not.toHaveProperty('cancellationReason');
  });
});
