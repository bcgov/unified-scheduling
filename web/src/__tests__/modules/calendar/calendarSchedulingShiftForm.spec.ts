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

    expect(formData.assignmentEntryIds).toEqual([251]);
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

  it('uses 15 minute increments for scheduling time options', () => {
    expect(timeOptions.filter((option) => String(option.code).startsWith('09:')).map((option) => option.code)).toEqual([
      '09:00',
      '09:15',
      '09:30',
      '09:45',
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

  it('includes selected assignment entries in the aggregate create request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        assignmentEntryIds: [42, 43],
      },
      'entry',
    );
    const payload = buildCreateShiftPayload({
      formData,
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('entry');
    expect(formData.assignmentEntryLinks).toEqual([
      {
        assignmentEntryId: 42,
        assignmentSeriesId: undefined,
        assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
      },
      {
        assignmentEntryId: 43,
        assignmentSeriesId: undefined,
        assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
      },
    ]);
    expect(payload?.body).toMatchObject({
      assignmentEntryLinks: [
        {
          assignmentEntryId: 42,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
        {
          assignmentEntryId: 43,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('preserves assignment entry link users in the aggregate request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        assignmentEntryLinks: [
          {
            assignmentEntryId: 42,
            assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
      },
      'entry',
    );
    const payload = buildCreateShiftPayload({
      formData,
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('entry');
    expect(formData.assignmentEntryLinks).toEqual([
      {
        assignmentEntryId: 42,
        assignmentSeriesId: undefined,
        assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
      },
    ]);
    expect(payload?.body).toMatchObject({
      assignmentEntryLinks: [
        {
          assignmentEntryId: 42,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('builds update payloads with the complete desired assignment links', () => {
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
        assignmentEntryIds: [278],
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
        assignmentEntryId: null,
        assignmentSeriesId: null,
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
    expect(result.data?.assignmentSeriesLinks).toBeUndefined();
    expect(normalized).not.toHaveProperty('assignmentEntryId');
    expect(normalized).not.toHaveProperty('assignmentSeriesId');
  });

  it('defaults omitted linked assignment users from the selected shift users before validation', () => {
    const normalized = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        assignmentEntryLinks: [{ assignmentEntryId: 278 }],
      },
      'entry',
    );
    const result = validateShiftFormData(normalized, { timeZoneId: 'America/Vancouver' });

    expect(result.errors).toEqual({});
    expect(result.data).toMatchObject({
      assignmentEntryLinks: [
        {
          assignmentEntryId: 278,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('coerces selected assignment entry ids into the aggregate request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        assignmentEntryIds: ['42', '43'] as unknown as number[],
      },
      'entry',
    );
    const payload = buildUpdateShiftPayload({
      formData,
      scope: 'entry',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
    });

    expect(payload?.kind).toBe('entry');
    expect(formData.assignmentEntryLinks?.map((link) => link.assignmentEntryId)).toEqual([42, 43]);
    expect(payload?.body).toMatchObject({
      assignmentEntryLinks: [
        expect.objectContaining({ assignmentEntryId: 42 }),
        expect.objectContaining({ assignmentEntryId: 43 }),
      ],
    });
  });

  it('includes selected assignment series in the aggregate create request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesId: 24,
      },
      'series',
    );
    const payload = buildCreateShiftPayload({
      formData,
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('series');
    expect(formData.assignmentSeriesLinks?.map((link) => link.assignmentSeriesId)).toEqual([24]);
    expect(payload?.body).toMatchObject({
      assignmentSeriesLinks: [expect.objectContaining({ assignmentSeriesId: 24 })],
    });
  });

  it('keeps untouched and explicitly cleared assignment series relationships distinct', () => {
    const untouched = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesId: undefined,
        assignmentSeriesLinks: undefined,
      },
      'series',
    );
    const cleared = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesId: null,
        assignmentSeriesLinks: [],
      },
      'series',
    );

    expect(untouched.assignmentSeriesLinks).toBeUndefined();
    expect(cleared.assignmentSeriesLinks).toEqual([]);
  });

  it('omits untouched assignment series relationships from update requests', () => {
    const payload = buildUpdateShiftPayload({
      formData: {
        ...baseFormData,
        description: 'Updated description',
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesLinks: [
          {
            assignmentSeriesId: 24,
            assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
          },
        ],
      },
      scope: 'series',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
      includeAssignmentSeriesLinks: false,
    });

    expect(payload?.body).not.toHaveProperty('assignmentSeriesLinks');
  });

  it('sends an empty assignment series relationship collection when links are explicitly cleared', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesId: null,
        assignmentSeriesLinks: [],
      },
      'series',
    );
    const payload = buildUpdateShiftPayload({
      formData,
      scope: 'series',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
      includeAssignmentSeriesLinks: true,
    });

    expect(payload?.body).toMatchObject({ assignmentSeriesLinks: [] });
  });

  it('preserves assignment series link users in the aggregate request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
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
      'series',
    );
    const payload = buildCreateShiftPayload({
      formData,
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
    });

    expect(payload?.kind).toBe('series');
    expect(formData.assignmentSeriesLinks).toEqual([
      {
        assignmentEntryId: undefined,
        assignmentSeriesId: 24,
        assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
      },
    ]);
    expect(payload?.body).toMatchObject({
      assignmentSeriesLinks: [
        {
          assignmentSeriesId: 24,
          assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
        },
      ],
    });
  });

  it('coerces selected assignment series ids into the aggregate request', () => {
    const formData = normalizeShiftFormDataForScope(
      {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        assignmentSeriesId: '24' as unknown as number,
      },
      'series',
    );
    const payload = buildUpdateShiftPayload({
      formData,
      scope: 'series',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
    });

    expect(payload?.kind).toBe('series');
    expect(formData.assignmentSeriesLinks?.map((link) => link.assignmentSeriesId)).toEqual([24]);
    expect(payload?.body).toMatchObject({
      assignmentSeriesLinks: [expect.objectContaining({ assignmentSeriesId: 24 })],
    });
  });

  it('omits untouched assignment relationships from update requests', () => {
    const payload = buildUpdateShiftPayload({
      formData: {
        ...baseFormData,
        assignmentEntryLinks: [{ assignmentEntryId: 278, assignedUserIds: baseFormData.userIds }],
      },
      scope: 'entry',
      timeZoneId: 'America/Vancouver',
      locationId: 1,
      fallbackTitle: 'System System',
      shiftSeriesId: 210,
      includeAssignmentEntryLinks: false,
    });

    expect(payload?.body).not.toHaveProperty('assignmentEntryLinks');
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
