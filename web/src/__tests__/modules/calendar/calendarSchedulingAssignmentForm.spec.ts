import { describe, expect, it } from 'vitest';
import {
  buildCreateAssignmentPayload,
  createInitialAssignmentFormData,
  resolveShiftEntryLinksFromAssignmentEntry,
  validateAssignmentFormData,
  type AssignmentFormData,
} from '@/modules/scheduling/calendarSchedulingAssignmentForm';

const baseFormData: AssignmentFormData = {
  ...createInitialAssignmentFormData('2026-07-12'),
  title: 'Court assignment',
  assignmentDefinitionId: 7,
  assignmentCategoryTypeId: 10,
  assignmentSubCategoryTypeId: 20,
  date: '2026-07-12',
  startTime: '09:00',
  endTime: '17:00',
  repeatMode: 'never',
  capacity: 1,
};

describe('calendarSchedulingAssignmentForm', () => {
  it('builds assignment entry payloads with a required location', () => {
    const payload = buildCreateAssignmentPayload({
      formData: baseFormData,
      timeZoneId: 'America/Vancouver',
      locationId: 12,
      assignmentOptions: [{ code: 7, description: 'Court assignment' }],
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.body).toMatchObject({
      assignmentDefinitionId: 7,
      locationId: 12,
    });
  });

  it('does not build assignment payloads without a location', () => {
    const payload = buildCreateAssignmentPayload({
      formData: baseFormData,
      timeZoneId: 'America/Vancouver',
      locationId: null,
      assignmentOptions: [{ code: 7, description: 'Court assignment' }],
    });

    expect(payload).toBeNull();
  });

  it('validates an existing assignment entry with no linked shifts', () => {
    const result = validateAssignmentFormData(
      {
        ...baseFormData,
        assignmentSeriesId: 200,
        seriesStartAtUtc: '2026-07-15T16:00:00+00:00',
        seriesEndAtUtc: '2026-07-16T00:00:00+00:00',
        shiftEntryIds: [],
        shiftEntryLinks: [],
      },
      { timeZoneId: 'America/Vancouver' },
    );

    expect(result.errors).toEqual({});
    expect(result.data).toMatchObject({
      shiftEntryLinks: [],
    });
  });

  it('builds assignment series link payloads using assigned user ids', () => {
    const payload = buildCreateAssignmentPayload({
      formData: {
        ...baseFormData,
        repeatMode: 'custom',
        recurrenceRule: 'RRULE:FREQ=WEEKLY;COUNT=4',
        shiftSeriesLinks: [
          {
            shiftSeriesId: 200,
            assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
          },
        ],
      },
      timeZoneId: 'America/Vancouver',
      locationId: 12,
      assignmentOptions: [{ code: 7, description: 'Court assignment' }],
    });

    expect(payload?.kind).toBe('series');
    expect(payload?.body).toMatchObject({
      shiftSeriesLinks: [
        {
          shiftSeriesId: 200,
          assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
        },
      ],
    });
  });

  it('preserves per-link assigned users from assignment entry responses', () => {
    expect(
      resolveShiftEntryLinksFromAssignmentEntry({
        id: 257,
        assignmentDefinitionId: 7,
        assignmentCategoryTypeId: 10,
        assignmentSubCategoryTypeId: 20,
        capacity: 1,
        linkedShiftEntryIds: [200, 201],
        assignedUserIds: [
          'feaa2a73-6898-48ae-9c32-9633b1ec5538',
          'd787ac4b-7969-4509-bc2b-9c85c4cbe3cb',
        ],
        assignmentLinks: [
          {
            id: 1,
            shiftEntryId: 200,
            assignmentEntryId: 257,
            userIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
          },
          {
            id: 2,
            shiftEntryId: 201,
            assignmentEntryId: 257,
            userIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
          },
        ],
      }),
    ).toEqual([
      {
        shiftEntryId: 200,
        assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
      },
      {
        shiftEntryId: 201,
        assignedUserIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
      },
    ]);
  });

  it('does not merge users across assignment entry links when saving unchanged edit data', () => {
    const shiftEntryLinks = resolveShiftEntryLinksFromAssignmentEntry({
      id: 257,
      assignmentDefinitionId: 7,
      assignmentCategoryTypeId: 10,
      assignmentSubCategoryTypeId: 20,
      capacity: 1,
      linkedShiftEntryIds: [200, 201],
      assignedUserIds: [
        'feaa2a73-6898-48ae-9c32-9633b1ec5538',
        'd787ac4b-7969-4509-bc2b-9c85c4cbe3cb',
      ],
      assignmentLinks: [
        {
          id: 1,
          shiftEntryId: 200,
          assignmentEntryId: 257,
          userIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
        },
        {
          id: 2,
          shiftEntryId: 201,
          assignmentEntryId: 257,
          userIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
        },
      ],
    });

    const payload = buildCreateAssignmentPayload({
      formData: {
        ...baseFormData,
        shiftEntryLinks,
      },
      timeZoneId: 'America/Vancouver',
      locationId: 12,
      assignmentOptions: [{ code: 7, description: 'Court assignment' }],
    });

    expect(payload?.kind).toBe('entry');
    expect(payload?.body).toMatchObject({
      shiftEntryLinks: [
        {
          shiftEntryId: 200,
          assignedUserIds: ['feaa2a73-6898-48ae-9c32-9633b1ec5538'],
        },
        {
          shiftEntryId: 201,
          assignedUserIds: ['d787ac4b-7969-4509-bc2b-9c85c4cbe3cb'],
        },
      ],
    });
  });
});
