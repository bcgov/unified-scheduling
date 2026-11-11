import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { updateAssignmentEntry } from '@/modules/scheduling/calendarSchedulingAssignmentApi';
import { isSchedulingConcurrencyConflict } from '@/modules/scheduling/schedulingConcurrency';

describe('scheduling concurrency errors', () => {
  it.each([
    { status: 409, body: { code: 'concurrency_conflict', detail: 'Reload first.' }, expected: true },
    { status: 409, body: { message: 'Scheduling conflict.', conflicts: [] }, expected: false },
    { status: 409, body: { errors: { UserIds: ['Duplicate shift.'] } }, expected: false },
    { status: 400, body: { code: 'concurrency_conflict' }, expected: false },
  ])('identifies only version conflicts: $status $body', async ({ status, body, expected }) => {
    let writes = 0;
    server.use(
      http.put('*/api/scheduling/assignments/entries/41', () => {
        writes++;
        return HttpResponse.json(body, { status });
      }),
    );

    const result = await updateAssignmentEntry(41, { concurrencyToken: 7 });

    expect(result.error.value).toBeInstanceOf(Error);
    expect(isSchedulingConcurrencyConflict(result)).toBe(expected);
    expect(writes).toBe(1);
  });
});
