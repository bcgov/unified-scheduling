import { describe, expect, it } from 'vitest';
import {
  buildCreateShiftPayload,
  buildCreateShiftPayloadWithErrors,
  buildUpdateShiftPayload,
  buildUpdateShiftPayloadWithErrors,
  createInitialShiftFormDataForCreateAction,
  createShiftFormDataFromEntry,
  normalizeShiftFormDataForScope,
  normalizeShiftFormTimes,
  validateShiftFormData,
  type ShiftResourceFormData,
} from '@/modules/scheduling/calendarSchedulingShiftForm';
import {
  formatTimeOptionRange,
  formatTimeOptionValue,
  normalizeTimeOptionValue,
  timeOptions,
} from '@/modules/scheduling/schedulingDateTime';

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
  it('hydrates linked assignments from a shift entry response', () => {
    const formData = createShiftFormDataFromEntry(
      {
        id: 42,
        title: 'Registry shift',
        startAtUtc: '2026-06-29T16:00:00Z',
        endAtUtc: '2026-06-30T00:00:00Z',
        assignmentLinks: [
          {
            assignmentEntryId: 251,
            userIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
      },
      {
        id: 'shift-42',
        type: 'scheduling.shift',
        sourceModule: 'scheduling',
        title: 'Registry shift',
        start: '2026-06-29T16:00:00Z',
        end: '2026-06-30T00:00:00Z',
      },
      'America/Vancouver',
    );

    expect(formData.assignmentEntryLinks).toEqual([
      {
        assignmentEntryId: 251,
        assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
      },
    ]);
  });

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

  it('normalizes backend time-only values to select option values', () => {
    expect(normalizeTimeOptionValue('09:00:00')).toBe('09:00');
    expect(normalizeTimeOptionValue('17:00:00')).toBe('17:00');

    expect(
      normalizeShiftFormTimes({
        ...baseFormData,
        startTime: '09:00:00',
        endTime: '17:00:00',
      }),
    ).toMatchObject({
      startTime: '09:00',
      endTime: '17:00',
    });
  });

  it('formats form time values using canonical options and Luxon fallbacks', () => {
    expect(formatTimeOptionValue('09:00:00')).toBe('9:00 AM');
    expect(formatTimeOptionValue('09:07:00')).toBe('9:07 AM');
    expect(formatTimeOptionValue('not-a-time')).toBe('not-a-time');
    expect(formatTimeOptionRange('09:00', '17:00')).toBe('9:00 AM - 5:00 PM');
    expect(formatTimeOptionRange(undefined, undefined)).toBe('Unknown');
  });

  it('uses 30 minute increments for scheduling time options', () => {
    expect(timeOptions.filter((option) => String(option.code).startsWith('09:')).map((option) => option.code)).toEqual([
      '09:00',
      '09:30',
    ]);
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

  it('does not build create payloads without a location', () => {
    const result = buildCreateShiftPayloadWithErrors({
      formData: baseFormData,
      timeZoneId: 'America/Vancouver',
      locationId: null,
      fallbackTitle: 'System System',
    });

    expect(result.payload).toBeNull();
    expect(result.errors).toEqual({ locationId: 'Required' });
  });

  it('adds assignment entry link user details to shift entry payloads', () => {
    const payload = buildCreateShiftPayload({
      formData: {
        ...baseFormData,
        assignmentEntryLinks: [
          {
            assignmentEntryId: 42,
            assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
      },
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.body).toMatchObject({
      assignmentEntryLinks: [
        {
          assignmentEntryId: 42,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('builds update payloads when a linked shift entry start time changes', () => {
    const payload = buildUpdateShiftPayload({
      formData: {
        ...baseFormData,
        title: 'Developer User shift',
        date: '2026-07-13',
        startTime: '09:30',
        endTime: '17:00',
        statusTypeCode: 'draft',
        locationId: 1,
        userIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
        assignmentEntryLinks: [
          {
            assignmentEntryId: 278,
            assignedUserIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
          },
        ],
      },
      scope: 'entry',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'Developer User shift',
      shiftSeriesId: 203,
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.body).toMatchObject({
      shiftSeriesId: 203,
      startAtUtc: '2026-07-13T16:30:00Z',
      endAtUtc: '2026-07-14T00:00:00Z',
      locationId: 1,
      userIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
      assignmentEntryLinks: [
        {
          assignmentEntryId: 278,
          assignedUserIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
        },
      ],
    });
  });

  it('does not validate hidden series assignment links when editing a shift entry', () => {
    const normalized = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        startTime: '09:30',
        assignmentEntryLinks: [
          {
            assignmentEntryId: 278,
            assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
        assignmentSeriesLinks: [
          {
            assignmentSeriesId: 24,
            assignedUserIds: [],
          },
        ],
      },
      'entry',
    );
    const result = validateShiftFormData(normalized, { timeZoneId: 'America/Vancouver' });

    expect(result.errors).toEqual({});
    expect(result.data).toMatchObject({
      assignmentSeriesLinks: [],
    });
  });

  it('adds assignment series link user details to shift series payloads', () => {
    const payload = buildCreateShiftPayload({
      formData: {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesLinks: [
          {
            assignmentSeriesId: 24,
            assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
      },
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('series');
    expect(payload?.body).toMatchObject({
      assignmentSeriesLinks: [
        {
          assignmentSeriesId: 24,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('keeps lifecycle fields out of update payloads', () => {
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

  it('does not build update payloads without a location', () => {
    const result = buildUpdateShiftPayloadWithErrors({
      formData: baseFormData,
      scope: 'entry',
      timeZoneId: 'America/Vancouver',
      locationId: null,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
    });

    expect(result.payload).toBeNull();
    expect(result.errors).toEqual({ locationId: 'Required' });
  });
});
