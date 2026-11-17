import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MockInstance } from 'vitest';
import type { createApp } from 'vue';
import { server } from '@/__tests__/mocks/server';

vi.mock('vue', async (importOriginal) => {
  const vue = await importOriginal<typeof import('vue')>();
  return { ...vue, createApp: vi.fn(vue.createApp) };
});
// MSW's node server handles HTTP; skip browser service-worker registration.
vi.mock('@/mocks', () => ({ setupMockServiceWorker: vi.fn().mockResolvedValue(undefined) }));
// Exercise real bootstrap and routing; LoggedOut.spec.ts covers the view and shell.
vi.mock('@/App.vue', () => ({ default: { template: '<div data-testid="bootstrap-ready" />' } }));

describe('Post-logout navigation', () => {
  let appCreation: MockInstance<typeof createApp>;
  let pageListeners: MockInstance<typeof window.addEventListener>;
  let authRequests: number;
  let configRequests: number;

  beforeEach(async () => {
    vi.resetModules();
    document.body.innerHTML = '<div id="app"></div>';
    authRequests = 0;
    configRequests = 0;
    appCreation = vi.mocked((await import('vue')).createApp);
    appCreation.mockClear();
    pageListeners = vi.spyOn(window, 'addEventListener');
    server.use(
      http.get('*/api/config', () => {
        configRequests += 1;
        return HttpResponse.json({ featureFlags: {} });
      }),
      http.get('*/api/auth/user', () => {
        authRequests += 1;
        return new HttpResponse(null, { status: 401 });
      }),
    );
  });

  function disposeApplication() {
    const app = appCreation.mock.results[0]?.value;
    if (app) {
      app.unmount();
      app.config.globalProperties.$router?.options.history.destroy();
    }
    for (const [type, listener, options] of pageListeners.mock.calls) {
      if (type === 'pageshow') window.removeEventListener(type, listener, options);
    }
    pageListeners.mockClear();
  }

  afterEach(() => {
    disposeApplication();
    vi.restoreAllMocks();
    vi.unstubAllEnvs();
    window.history.replaceState({}, '', '/');
    document.body.innerHTML = '';
  });

  it.each([
    ['/', '/'],
    ['/unified-scheduling/', '/unified-scheduling/'],
    ['/', '/dashboard?location=2#week'],
    ['/', '/logged-out/other'],
    ['/', '/logged-out-other'],
  ])('keeps normal automatic login at %s when visiting %s', async (baseUrl, returnPath) => {
    vi.stubEnv('BASE_URL', baseUrl);
    window.history.replaceState({}, '', returnPath);

    await import('@/main');

    await vi.waitFor(() => {
      const loginUrl = new URL(window.location.href);
      expect(loginUrl.pathname).toBe(`${baseUrl}api/auth/login`);
      expect(loginUrl.searchParams.get('redirectUri')).toBe(returnPath);
    });
    expect(authRequests).toBe(1);
  });

  it.each([
    ['/', '/logged-out/?source=logout#confirmation'],
    ['/unified-scheduling/', '/unified-scheduling/logged-out'],
  ])(
    'boots the public logout page at %s without authentication on entry or refresh at %s',
    async (baseUrl, returnPath) => {
      vi.stubEnv('BASE_URL', baseUrl);
      window.history.replaceState({}, '', returnPath);

      for (let visit = 1; visit <= 2; visit += 1) {
        await import('@/main');
        await vi.waitFor(() => {
          expect(document.querySelector('[data-testid="bootstrap-ready"]')).not.toBeNull();
        });
        const router = appCreation.mock.results[0].value.config.globalProperties.$router;
        expect(router.currentRoute.value.name).toBe('LoggedOut');
        expect(window.location.href).toBe(new URL(returnPath, window.location.origin).href);
        expect(configRequests).toBe(visit);
        expect(authRequests).toBe(0);

        if (visit === 1) {
          // Discard application/module state at the same URL to model refresh startup.
          disposeApplication();
          appCreation.mockClear();
          vi.resetModules();
          appCreation = vi.mocked((await import('vue')).createApp);
          document.body.innerHTML = '<div id="app"></div>';
        }
      }
    },
  );
});
