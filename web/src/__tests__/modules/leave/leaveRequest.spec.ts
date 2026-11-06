import { describe, expect, it } from 'vitest';
import { buildLeaveRequest, buildLeaveTitle } from '@/modules/leave/leaveRequest';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';

const entryForm: LeaveFormData = {
  userIds: ['user-1'],
  leaveType: 1,
  date: '2026-06-10',
  startTime: '19:00',
  endTime: null,
};
const requestOptions = { locationId: 3, timeZoneId: 'America/Vancouver' };

describe('leaveRequest', () => {
  it('serializes an intentionally absent entry end as null even when the start is after 17:00', () => {
    const result = buildLeaveRequest({ ...entryForm, notes: 'Updated notes' }, requestOptions);

    expect(result).toMatchObject({
      kind: 'entry',
      request: { startAtUtc: '2026-06-11T02:00:00Z', endAtUtc: null, notes: 'Updated notes' },
    });
    expect(JSON.parse(JSON.stringify(result))).toHaveProperty('request.endAtUtc', null);
  });

  it('serializes an explicitly selected end time', () => {
    expect(buildLeaveRequest({ ...entryForm, endTime: '20:00' }, requestOptions)).toMatchObject({
      kind: 'entry',
      request: { startAtUtc: '2026-06-11T02:00:00Z', endAtUtc: '2026-06-11T03:00:00Z' },
    });
  });

  it.each([undefined, ''])('retains creation defaults for uninitialized times (%s)', (time) => {
    expect(buildLeaveRequest({ ...entryForm, startTime: time, endTime: time }, requestOptions)).toMatchObject({
      kind: 'entry',
      request: { startAtUtc: '2026-06-10T16:00:00Z', endAtUtc: '2026-06-11T00:00:00Z' },
    });
  });

  it.each([
    { endTime: 'not-a-time', error: 'Select a valid date and time.' },
    { endTime: '18:00', error: 'End time must be after start time.' },
    { endTime: '19:00', error: 'End time must be after start time.' },
  ])('rejects an explicitly supplied invalid entry end ($endTime)', ({ endTime, error }) => {
    expect(buildLeaveRequest({ ...entryForm, endTime }, requestOptions)).toEqual({ error });
  });

  it.each([
    { endTime: null, error: 'Select a valid date and time.' },
    { endTime: 'not-a-time', error: 'Select a valid date and time.' },
    { endTime: '18:00', error: 'End time must be after start time.' },
    { endTime: '19:00', error: 'End time must be after start time.' },
  ])('requires a valid ordered end for recurring leave ($endTime)', ({ endTime, error }) => {
    expect(
      buildLeaveRequest(
        {
          ...entryForm,
          endTime,
          repeatMode: 'custom',
          recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        },
        requestOptions,
      ),
    ).toEqual({ error });
  });

  it('accepts recurring leave with an explicitly supplied end', () => {
    expect(
      buildLeaveRequest(
        {
          ...entryForm,
          endTime: '20:00',
          repeatMode: 'custom',
          recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        },
        requestOptions,
      ),
    ).toMatchObject({
      kind: 'series',
      request: { endAtUtc: '2026-06-11T03:00:00Z', recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2' },
    });
  });

  it('retains creation defaults when creating recurring leave', () => {
    expect(
      buildLeaveRequest(
        {
          ...entryForm,
          startTime: undefined,
          endTime: undefined,
          repeatMode: 'custom',
          recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=2',
        },
        requestOptions,
      ),
    ).toMatchObject({
      kind: 'series',
      request: { startAtUtc: '2026-06-10T16:00:00Z', endAtUtc: '2026-06-11T00:00:00Z' },
    });
  });

  it('includes selected employee names in the leave event title', () => {
    const result = buildLeaveRequest(
      {
        userIds: ['user-1', 'user-2'],
        leaveType: 1,
        date: '2026-06-29',
        startTime: '09:00',
        endTime: '17:00',
      },
      {
        locationId: 1,
        timeZoneId: 'America/Vancouver',
        leaveSeriesId: 42,
        employeeOptions: [
          { code: 'user-1', description: 'Alex Smith' },
          { code: 'user-2', description: 'Sam Lee' },
        ],
      },
    );

    expect(result).toMatchObject({
      kind: 'entry',
      request: { title: 'Alex Smith, Sam Lee leave', leaveSeriesId: 42 },
    });
  });

  it('falls back to employee ids when a selected employee name is unavailable', () => {
    expect(buildLeaveTitle(['user-1', '  '])).toBe('user-1 leave');
  });
});
