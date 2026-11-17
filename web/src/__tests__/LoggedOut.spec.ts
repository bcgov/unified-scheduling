import { mount } from '@vue/test-utils';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { MockInstance } from 'vitest';
import { nextTick } from 'vue';
import App from '@/App.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useConfigStore } from '@/stores/config';

describe('Logged-out landing page', () => {
  let app: Awaited<ReturnType<typeof createTestApp>>;
  let wrapper: ReturnType<typeof mount> | undefined;
  let pageListeners: MockInstance<typeof window.addEventListener>;

  beforeEach(() => {
    window.history.replaceState({}, '', '/');
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
    vi.unstubAllEnvs();
    window.history.replaceState({}, '', '/');
  });

  async function openLoggedOutPage(isAuthenticated = false) {
    app = await createTestApp({ isAuthenticated, isRegistered: isAuthenticated });
    useConfigStore(app.pinia).config = {
      featureFlags: {},
      selectedBrand: 'bcss',
      brandingOptions: {
        bcss: {
          applicationName: 'BC Sheriff Scheduling',
          primaryLogoUrl: '/images/primary.svg',
          primaryLogoAlt: 'Configured primary logo',
          secondaryLogoUrl: '/images/secondary.svg',
          secondaryLogoAlt: 'Configured secondary logo',
          backgroundImageUrl: '/images/overlay.svg',
        },
      },
    };
    await app.router.push('/logged-out');
    await app.router.isReady();
    wrapper = mount(App, { global: { plugins: app.mountPlugins } });
    await nextTick();
  }

  it.each([
    ['/', false],
    ['/unified-scheduling/', true],
  ] as const)(
    'renders the branded shell and root login link at %s without authenticated controls (cached user: %s)',
    async (baseUrl, cachedUser) => {
      vi.stubEnv('BASE_URL', baseUrl);
      await openLoggedOutPage(cachedUser);

      expect(wrapper!.get('h1').text()).toBe('You have been logged out');
      expect(wrapper!.text()).toContain('BC Sheriff Scheduling');
      expect(wrapper!.text()).toContain('Your session has ended. Log in to continue.');
      expect(wrapper!.get('.appbar-logo--primary img').attributes()).toMatchObject({
        src: `${baseUrl}images/primary.svg`,
        alt: 'Configured primary logo',
      });
      expect(wrapper!.get('.appbar-logo--secondary img').attributes('src')).toBe(`${baseUrl}images/secondary.svg`);
      expect(wrapper!.find('.ua-background-overlay__image').exists()).toBe(true);
      expect(wrapper!.find('footer.app-footer').exists()).toBe(true);
      expect(wrapper!.find('a.appbar-branding-link').exists()).toBe(false);
      expect(wrapper!.find('.router-link-container').exists()).toBe(false);
      expect(wrapper!.find('.appbar-mobile-navigation').exists()).toBe(false);
      expect(wrapper!.find('.appbar-actions').exists()).toBe(false);
      expect(wrapper!.find('.dashboard').exists()).toBe(false);
      const login = wrapper!.get('a');
      expect(login.text()).toBe('Log in');
      expect(login.attributes('href')).toBe(baseUrl);
      expect(login.classes()).toContain('v-btn');
    },
  );
});
