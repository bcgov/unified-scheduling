import { describe, expect, it } from 'vitest';
import { buildLeaveRequest, buildLeaveTitle } from '@/modules/leave/leaveRequest';

describe('leaveRequest', () => {
  it('submits an all-day date without temporarily retained times', () => {
    const result = buildLeaveRequest(
      { userIds: ['user-1'], leaveType: 1, date: '2026-03-08', allDay: true, startTime: '17:00', endTime: '09:00' },
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
    if ('request' in result) expect(result.request).not.toHaveProperty('leaveDate');
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
