import { mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MockInstance } from 'vitest';
import { nextTick } from 'vue';
import App from '@/App.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { useAuthStore } from '@/stores/auth';
import * as authNavigation from '@/utils/authNavigation';

describe('Protected pages after logout', () => {
  let app: Awaited<ReturnType<typeof createTestApp>>;
  let wrapper: ReturnType<typeof mount> | undefined;
  let authRequests: number;
  let pageListeners: MockInstance<typeof window.addEventListener>;

  beforeEach(() => {
    window.history.replaceState({}, '', '/');
    authRequests = 0;
    pageListeners = vi.spyOn(window, 'addEventListener');
  });

  afterEach(() => {
    wrapper?.unmount();
    wrapper = undefined;
    app?.router.options.history.destroy();
    for (const [type, listener, options] of pageListeners.mock.calls) {
      if (type === 'pageshow') window.removeEventListener(type, listener, options);
    }
    vi.restoreAllMocks();
  });

  async function openProtectedPage() {
    app = await createTestApp();
    app.router.addRoute({
      path: '/protected-test',
      component: { template: '<div data-testid="secured-content">Confidential schedule</div>' },
      meta: { requiresAuth: true },
    });
    await app.router.push('/protected-test?location=2#week');
    await app.router.isReady();
    wrapper = mount(App, {
      global: {
        plugins: app.mountPlugins,
        stubs: { Appbar: true, AppFooter: true },
      },
    });
    expect(wrapper.find('[data-testid="secured-content"]').exists()).toBe(true);
    expect(wrapper.find('appbar-stub').exists()).toBe(true);
  }

  function showPage(persisted: boolean) {
    const event = new Event('pageshow');
    Object.defineProperty(event, 'persisted', { value: persisted });
    window.dispatchEvent(event);
  }

  function respondToAuth(response: () => Response) {
    server.use(
      http.get('*/api/auth/user', () => {
        authRequests += 1;
        return response();
      }),
    );
  }

  function expectLoginRedirect() {
    const loginUrl = new URL(window.location.href);
    expect(loginUrl.pathname).toBe('/api/auth/login');
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/protected-test?location=2#week');
  }

  it.each([
    ['401', () => new HttpResponse(null, { status: 401 })],
    ['an unauthenticated user', () => HttpResponse.json({ isAuthenticated: false, isRegistered: false })],
  ])(
    'hides a back/forward-cache restored page and redirects to login when the API returns %s',
    async (_label, response) => {
      await openProtectedPage();
      respondToAuth(response);

      showPage(true);
      await nextTick();

      expect(useAuthStore(app.pinia).isAuthenticated).toBe(false);
      expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
      expect(wrapper!.find('appbar-stub').exists()).toBe(false);
      await vi.waitFor(expectLoginRedirect);
      expect(authRequests).toBe(1);
    },
  );

  it.each([
    ['503', () => new HttpResponse(null, { status: 503 })],
    ['a network error', () => HttpResponse.error()],
  ])('keeps restored secured content hidden when the session check fails with %s', async (_label, response) => {
    await openProtectedPage();
    respondToAuth(response);

    showPage(true);

    await vi.waitFor(() => {
      expect(app.router.currentRoute.value.path).toBe('/application-unavailable');
    });
    expect(useAuthStore(app.pinia).isAuthenticated).toBe(false);
    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
    expect(authRequests).toBe(1);
  });

  it('hides the topbar immediately when the user logs out', async () => {
    await openProtectedPage();

    useAuthStore(app.pinia).clearUserInfo();
    await nextTick();

    expect(wrapper!.find('appbar-stub').exists()).toBe(false);
    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
  });

  it('blocks direct navigation to a secured URL after local logout', async () => {
    await openProtectedPage();
    await app.router.push('/application-unavailable');
    useAuthStore(app.pinia).clearUserInfo();
    respondToAuth(() => new HttpResponse(null, { status: 401 }));

    await app.router.push('/protected-test?location=2#week');

    expectLoginRedirect();
    expect(app.router.currentRoute.value.path).toBe('/application-unavailable');
    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
    expect(authRequests).toBe(1);
  });

  it('blocks browser history Back to a secured page after local logout', async () => {
    await openProtectedPage();
    const securedPageState = window.history.state;
    await app.router.push('/application-unavailable');
    useAuthStore(app.pinia).clearUserInfo();
    respondToAuth(() => new HttpResponse(null, { status: 401 }));
    const redirect = vi.spyOn(authNavigation, 'redirectToLogin').mockImplementation(() => {});

    window.history.replaceState(securedPageState, '', '/protected-test?location=2#week');
    window.dispatchEvent(new PopStateEvent('popstate', { state: securedPageState }));

    await vi.waitFor(() => {
      expect(redirect).toHaveBeenCalledWith('/protected-test?location=2#week');
    });
    expect(app.router.currentRoute.value.path).toBe('/application-unavailable');
    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
    expect(authRequests).toBe(1);
  });

  it('does not recheck authentication on a normal pageshow event', async () => {
    await openProtectedPage();
    respondToAuth(() => new HttpResponse(null, { status: 401 }));

    showPage(false);
    await nextTick();

    expect(authRequests).toBe(0);
    expect(useAuthStore(app.pinia).isAuthenticated).toBe(true);
    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(true);
  });

  it('does not check authentication when a public page is restored', async () => {
    await openProtectedPage();
    await app.router.push('/application-unavailable');
    respondToAuth(() => new HttpResponse(null, { status: 401 }));

    showPage(true);
    await nextTick();

    expect(authRequests).toBe(0);
    expect(app.router.currentRoute.value.path).toBe('/application-unavailable');
  });

  it('displays restored secured content only after the API confirms the session is still valid', async () => {
    await openProtectedPage();
    const userInfo = useAuthStore(app.pinia).userInfo;
    respondToAuth(() => HttpResponse.json(userInfo));

    showPage(true);
    await nextTick();

    expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(false);
    expect(wrapper!.find('appbar-stub').exists()).toBe(false);
    await vi.waitFor(() => {
      expect(wrapper!.find('[data-testid="secured-content"]').exists()).toBe(true);
      expect(wrapper!.find('appbar-stub').exists()).toBe(true);
    });
    expect(app.router.currentRoute.value.fullPath).toBe('/protected-test?location=2#week');
    expect(authRequests).toBe(1);
  });
});
