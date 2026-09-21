import { describe, expect, it } from 'vitest';
import { formatAssigneeIds } from '@/modules/scheduling/calendarSchedulingShiftDetailRows';

describe('formatAssigneeIds', () => {
  it('formats known and unknown assignees', () => {
    expect(formatAssigneeIds(['user-1', 'user-2'], [{ code: 'user-1', description: 'Avery Chen' }])).toBe(
      'Avery Chen, user-2',
    );
  });

  it('formats an empty assignee list', () => {
    expect(formatAssigneeIds([], [])).toBe('None');
  });
});
