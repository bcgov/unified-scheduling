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
  it('submits an all-day date without temporarily retained times', () => {
    const result = buildLeaveRequest(
      { ...entryForm, date: '2026-03-08', allDay: true, endDate: '2026-03-09', endTime: '09:00' },
      { locationId: 1, timeZoneId: 'Asia/Tokyo' },
    );
    expect(result).toMatchObject({ kind: 'entry', request: { allDay: true, leaveDate: '2026-03-08' } });
    if ('request' in result) {
      expect(result.request).not.toHaveProperty('startAtUtc');
      expect(result.request).not.toHaveProperty('endAtUtc');
    }
  });

  it('builds all-day recurring requests from the selected date', () => {
    const result = buildLeaveRequest(
      {
        userIds: ['user-1'],
        leaveType: 1,
        date: '2026-11-01',
        allDay: true,
        repeatMode: 'custom',
        recurrenceRule: 'FREQ=DAILY;COUNT=3',
      },
      { locationId: 1, timeZoneId: 'America/Vancouver' },
    );
    expect(result).toMatchObject({
      kind: 'series',
      request: { allDay: true, leaveDate: '2026-11-01', recurrenceRule: 'FREQ=DAILY;COUNT=3' },
    });
  });

  it.each(['', '2026-02-30', '2026-03-08T00:00:00Z'])('rejects invalid all-day date %s', (date) => {
    expect(
      buildLeaveRequest(
        { userIds: ['user-1'], leaveType: 1, date, allDay: true },
        { locationId: 1, timeZoneId: 'America/Vancouver' },
      ),
    ).toEqual({ error: 'Select a valid date.' });
  });

  it('keeps timed requests free of date-only input', () => {
    const result = buildLeaveRequest(
      { userIds: ['user-1'], leaveType: 1, date: '2026-06-01', startTime: '09:00', endTime: '17:00' },
      { locationId: 1, timeZoneId: 'America/Vancouver' },
    );
    expect(result).toMatchObject({
      kind: 'entry',
      request: { allDay: false, startAtUtc: '2026-06-01T16:00:00Z', endAtUtc: '2026-06-02T00:00:00Z' },
    });
    if ('request' in result) {
      expect(result.request).not.toHaveProperty('leaveDate');
      expect(result.request).not.toHaveProperty('description');
    }
  });

  // Use Vancouver's 2025 fall-back before B.C. adopted permanent daylight time.
  it.each([
    { startAtUtc: '2025-11-02T08:30:00.123456Z', endAtUtc: '2025-11-02T09:30:00.654321Z' },
    { startAtUtc: '2025-11-02T08:30:00.123456Z', endAtUtc: '2025-11-02T08:30:00.123457Z' },
  ])('preserves distinct instants shown as the same local minute ($endAtUtc)', ({ startAtUtc, endAtUtc }) => {
    expect(
      buildLeaveRequest(
        { ...entryForm, date: '2025-11-02', startTime: '01:30', endDate: '2025-11-02', endTime: '01:30' },
        { ...requestOptions, persistedLeave: { startAtUtc, endAtUtc } },
      ),
    ).toMatchObject({ kind: 'entry', request: { startAtUtc, endAtUtc } });
  });

  const overnightForm = { ...entryForm, startTime: '22:00', endDate: '2026-06-11', endTime: '06:00' };
  const overnight = { startAtUtc: '2026-06-11T05:00:00.123456Z', endAtUtc: '2026-06-11T13:00:00.654321Z' };

  it.each([
    { change: { date: '2026-06-09' }, interval: { ...overnight, startAtUtc: '2026-06-10T05:00:00Z' } },
    { change: { startTime: '21:00' }, interval: { ...overnight, startAtUtc: '2026-06-11T04:00:00Z' } },
    { change: { endDate: '2026-06-12' }, interval: { ...overnight, endAtUtc: '2026-06-12T13:00:00Z' } },
    { change: { endTime: '07:00' }, interval: { ...overnight, endAtUtc: '2026-06-11T14:00:00Z' } },
  ])('updates $change while keeping the other endpoint exact', ({ change, interval }) => {
    expect(
      buildLeaveRequest({ ...overnightForm, ...change }, { ...requestOptions, persistedLeave: overnight }),
    ).toMatchObject({ kind: 'entry', request: interval });
  });

  it.each([
    { endDate: '', error: 'Select a valid date and time.' },
    { endDate: '2026-02-30', error: 'Select a valid date and time.' },
    { endDate: '2026-06-10', error: 'End time must be after start time.' },
  ])('rejects an invalid end date ($endDate)', ({ endDate, error }) => {
    expect(
      buildLeaveRequest({ ...overnightForm, endDate }, { ...requestOptions, persistedLeave: overnight }),
    ).toEqual({ error });
  });

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
