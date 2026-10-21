import type { UserInfo } from '@/api-access/generated/models';
import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { server } from '../mocks/server';

const authenticatedUser = {
  isAuthenticated: true,
  isRegistered: true,
  name: 'Test User',
  authenticationType: 'Cookies',
  claims: [],
  permissions: [],
  userId: 'b55f7b6b-36c7-4334-a3fd-cd8774b8ee77',
  homeLocationId: 1,
} as UserInfo;

const loadApi = async (withPinia = false) => {
  const [{ useFetchAPI }, piniaModule, { useAuthStore }] = await Promise.all([
    import('@/api-access/useFetchAPI'),
    import('pinia'),
    import('@/stores/auth'),
  ]);
  piniaModule.setActivePinia(undefined);

  if (!withPinia) {
    return { useFetchAPI, authStore: undefined };
  }

  const pinia = piniaModule.createPinia();
  piniaModule.setActivePinia(pinia);
  const authStore = useAuthStore(pinia);
  authStore.setUserInfo(authenticatedUser);
  return { useFetchAPI, authStore };
};

describe('useFetchAPI', () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, '', '/dashboard?location=2#summary');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('redirects a protected API 401 once, clears auth state, and preserves the return URL', async () => {
    server.use(http.get('*/api/protected', () => HttpResponse.json({}, { status: 401 })));
    const { useFetchAPI, authStore } = await loadApi(true);
    const clearUserInfo = vi.spyOn(authStore!, 'clearUserInfo');

    const first = useFetchAPI<void>({ url: '/api/protected', method: 'GET' }, { options: { immediate: false } });
    const second = useFetchAPI<void>({ url: '/api/protected', method: 'GET' }, { options: { immediate: false } });
    await Promise.all([first.execute(), second.execute()]);

    expect(clearUserInfo).toHaveBeenCalledTimes(1);
    expect(authStore!.userInfo).toBeNull();
    const loginUrl = new URL(window.location.href);
    expect(loginUrl.pathname).toBe('/api/auth/login');
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/dashboard?location=2#summary');
  });

  it('handles a protected API 401 when Pinia is unavailable', async () => {
    server.use(http.get('*/api/protected', () => HttpResponse.json({}, { status: 401 })));
    const { useFetchAPI } = await loadApi();
    const request = useFetchAPI<void>(
      { url: '/api/protected', method: 'GET' },
      { options: { immediate: false } },
    );

    await expect(request.execute()).resolves.toBeDefined();
    expect(new URL(window.location.href).pathname).toBe('/api/auth/login');
  });

  it.each(['/api/auth/user', '/api/auth/logout'])('does not redirect auth endpoint 401s for %s', async (url) => {
    server.use(http.get(`*${url}`, () => HttpResponse.json({}, { status: 401 })));
    const { useFetchAPI } = await loadApi();
    const request = useFetchAPI<void>({ url, method: 'GET' }, { options: { immediate: false } });

    await request.execute();

    expect(window.location.pathname).toBe('/dashboard');
  });

  it.each([
    ['forbidden', 403],
    ['server error', 500],
  ])('does not redirect or clear auth state for %s responses', async (_name, status) => {
    server.use(http.get('*/api/failure', () => HttpResponse.json({}, { status })));
    const { useFetchAPI, authStore } = await loadApi(true);
    const clearUserInfo = vi.spyOn(authStore!, 'clearUserInfo');
    const request = useFetchAPI<void>({ url: '/api/failure', method: 'GET' }, { options: { immediate: false } });

    await request.execute();

    expect(clearUserInfo).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/dashboard');
  });

  it('does not redirect or clear auth state for network failures', async () => {
    server.use(http.get('*/api/network-failure', () => HttpResponse.error()));
    const { useFetchAPI, authStore } = await loadApi(true);
    const clearUserInfo = vi.spyOn(authStore!, 'clearUserInfo');
    const request = useFetchAPI<void>(
      { url: '/api/network-failure', method: 'GET' },
      { options: { immediate: false } },
    );

    await request.execute();

    expect(clearUserInfo).not.toHaveBeenCalled();
    expect(window.location.pathname).toBe('/dashboard');
  });

  it('uses the configured application base path for the login endpoint', async () => {
    vi.stubEnv('BASE_URL', '/unified-scheduling/');
    server.use(http.get('*/api/protected', () => HttpResponse.json({}, { status: 401 })));
    const { useFetchAPI } = await loadApi();
    const request = useFetchAPI<void>(
      { url: '/api/protected', method: 'GET' },
      { options: { immediate: false } },
    );

    await request.execute();

    expect(new URL(window.location.href).pathname).toBe('/unified-scheduling/api/auth/login');
  });

  it('surfaces plain text error responses and invokes caller onFetchError', async () => {
    server.use(
      http.put('*/api/plain-text-error', () =>
        HttpResponse.text('Shift status changes must use the publish or expire endpoints.', { status: 400 }),
      ),
    );
    const { useFetchAPI } = await loadApi();
    const onFetchError = vi.fn((context) => context);
    const request = useFetchAPI<void>(
      { url: '/api/plain-text-error', method: 'PUT' },
      { options: { immediate: false, onFetchError } },
    );

    await request.execute();

    expect(request.error.value).toBeInstanceOf(Error);
    expect(request.error.value.message).toBe('Shift status changes must use the publish or expire endpoints.');
    expect(onFetchError).toHaveBeenCalledOnce();
  });

  it('parses json error and success responses', async () => {
    server.use(
      http.get('*/api/json-error', () => HttpResponse.json({ detail: 'JSON failure' }, { status: 400 })),
      http.get('*/api/json-success', () => HttpResponse.json({ ok: true })),
    );
    const { useFetchAPI } = await loadApi();
    const failed = useFetchAPI<void>({ url: '/api/json-error', method: 'GET' }, { options: { immediate: false } });
    const successful = useFetchAPI<{ ok: boolean }>(
      { url: '/api/json-success', method: 'GET' },
      { options: { immediate: false } },
    );

    await failed.execute();
    await successful.execute();

    expect(failed.error.value.message).toBe('JSON failure');
    expect(successful.error.value).toBeNull();
    expect(successful.data.value).toEqual({ ok: true });
  });
});
