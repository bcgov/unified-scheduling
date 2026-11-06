import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { useSchedulingAssignmentMutation } from '@/modules/scheduling/useSchedulingAssignmentMutation';

// Exercise the generated clients and fetch adapter, including actual JSON serialization.
describe('useSchedulingAssignmentMutation', () => {
  it.each(['entry', 'series'] as const)('rejects an edited %s with no ID', (kind) => {
    const mutation = useSchedulingAssignmentMutation();
    expect(() => mutation.save({ payload: { kind, body: {} }, isEdit: true, concurrencyToken: 7 })).toThrow(
      kind === 'entry'
        ? 'An assignment entry ID is required when editing an entry.'
        : 'An assignment series ID is required when editing a series.',
    );
  });

  it.each(['entry', 'series'] as const)('requires a loaded version when editing an %s', (kind) => {
    expect(() =>
      useSchedulingAssignmentMutation().save({
        payload: { kind, body: {} },
        isEdit: true,
        assignmentEntryId: 41,
        assignmentSeriesId: 42,
      }),
    ).toThrow('Reload the assignment before saving. Its version is missing.');
  });

  it.each([
    { kind: 'entry' as const, isEdit: false },
    { kind: 'series' as const, isEdit: false },
    { kind: 'entry' as const, isEdit: true },
    { kind: 'series' as const, isEdit: true },
  ])('sends the correct contract for $kind when isEdit is $isEdit', async ({ kind, isEdit }) => {
    const requests: Array<{ method: string; path: string; body: unknown }> = [];
    server.use(
      http.all('*/api/scheduling/assignments/*', async ({ request }) => {
        requests.push({ method: request.method, path: new URL(request.url).pathname, body: await request.json() });
        return HttpResponse.json({ id: 41, concurrencyToken: 8 });
      }),
    );

    const result = await useSchedulingAssignmentMutation().save({
      payload: { kind, body: { title: 'Court coverage' } },
      isEdit,
      concurrencyToken: 7,
      assignmentEntryId: 41,
      assignmentSeriesId: 42,
    });

    const scope = kind === 'entry' ? 'entries' : 'series';
    expect(requests).toEqual([
      {
        method: isEdit ? 'PUT' : 'POST',
        path: `/api/scheduling/assignments/${scope}${isEdit ? (kind === 'entry' ? '/41' : '/42') : ''}`,
        body: isEdit ? { title: 'Court coverage', concurrencyToken: 7 } : { title: 'Court coverage' },
      },
    ]);
    expect(result.error.value).toBeNull();
    expect(result.data.value?.concurrencyToken).toBe(8);
  });
});
