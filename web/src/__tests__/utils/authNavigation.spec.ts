import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const loadAuthNavigation = async (baseUrl: string) => {
  vi.stubEnv('BASE_URL', baseUrl);
  vi.resetModules();
  return import('@/utils/authNavigation');
};

describe('authNavigation', () => {
  beforeEach(() => {
    window.history.replaceState({}, '', '/dashboard');
  });

  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('preserves a router return path for a root deployment', async () => {
    const { redirectToLogin } = await loadAuthNavigation('/');

    redirectToLogin('/schedule?location=2#week');

    const loginUrl = new URL(window.location.href);
    expect(loginUrl.pathname).toBe('/api/auth/login');
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/schedule?location=2#week');
  });

  it('adds the application base to router-local paths in a subpath deployment', async () => {
    const { redirectToLogin } = await loadAuthNavigation('/unified-scheduling/');

    redirectToLogin('/schedule?location=2#week');

    const loginUrl = new URL(window.location.href);
    expect(loginUrl.pathname).toBe('/unified-scheduling/api/auth/login');
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/unified-scheduling/schedule?location=2#week');
  });

  it('does not duplicate an existing application base', async () => {
    const { redirectToLogin } = await loadAuthNavigation('/unified-scheduling/');

    redirectToLogin('/unified-scheduling/schedule?location=2#week');

    const loginUrl = new URL(window.location.href);
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/unified-scheduling/schedule?location=2#week');
  });

  it('uses the current local URL when login is requested without a return path', async () => {
    window.history.replaceState({}, '', '/unified-scheduling/dashboard?location=2#summary');
    const { redirectToLogin } = await loadAuthNavigation('/unified-scheduling/');

    redirectToLogin();

    const loginUrl = new URL(window.location.href);
    expect(loginUrl.searchParams.get('redirectUri')).toBe('/unified-scheduling/dashboard?location=2#summary');
  });

  it('keeps logout navigation unchanged', async () => {
    const { redirectToLogout } = await loadAuthNavigation('/unified-scheduling/');

    redirectToLogout();

    const logoutUrl = new URL(window.location.href);
    expect(logoutUrl.pathname).toBe('/unified-scheduling/api/auth/logout');
    expect(logoutUrl.search).toBe('');
  });
});
