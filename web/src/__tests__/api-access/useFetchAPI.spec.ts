import { useFetchAPI } from '@/api-access/useFetchAPI';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { server } from '../mocks/server';

describe('useFetchAPI', () => {
  it('surfaces plain text error responses as error messages', async () => {
    server.use(
      http.put('*/api/plain-text-error', () =>
        HttpResponse.text('Shift status changes must use the publish or expire endpoints.', { status: 400 }),
      ),
    );

    const request = useFetchAPI<void>(
      { url: '/api/plain-text-error', method: 'PUT' },
      { options: { immediate: false } },
    );

    await request.execute();

    expect(request.error.value).toBeInstanceOf(Error);
    expect(request.error.value.message).toBe('Shift status changes must use the publish or expire endpoints.');
  });

  it('parses json success responses', async () => {
    server.use(http.get('*/api/json-success', () => HttpResponse.json({ ok: true })));

    const request = useFetchAPI<{ ok: boolean }>(
      { url: '/api/json-success', method: 'GET' },
      { options: { immediate: false } },
    );

    await request.execute();

    expect(request.error.value).toBeNull();
    expect(request.data.value).toEqual({ ok: true });
  });
});
