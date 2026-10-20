import { describe, expect, it } from 'vitest';
import { buildLeaveRequest, buildLeaveTitle } from '@/modules/leave/leaveRequest';

describe('leaveRequest', () => {
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
