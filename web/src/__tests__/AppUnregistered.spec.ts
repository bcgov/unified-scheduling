import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { nextTick } from 'vue';
import App from '@/App.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

describe('App unregistered route', () => {
  it('redirects authenticated unregistered users to the unregistered page', async () => {
    const app = await createTestApp({
      isAuthenticated: true,
      isRegistered: false,
      supportEmail: 'support@example.com',
      applicationName: 'Unified Scheduling',
    });

    await app.router.push('/dashboard');
    await app.router.isReady();

    const wrapper = mount(App, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await nextTick();

    expect(app.router.currentRoute.value.path).toBe('/unregistered');
    expect(wrapper.find('.app-bar-wrapper').exists()).toBe(true);
    expect(wrapper.find('.router-link-container').exists()).toBe(false);
    expect(wrapper.find('.ua-card').exists()).toBe(true);
    expect(wrapper.find('.unregistered-page__title').text()).toBe('Welcome to the Unified Scheduling application.');
    expect(wrapper.text()).toContain('Welcome to the Unified Scheduling application.');
    expect(wrapper.text()).toContain('You are not currently registered to use this application.');
    expect(wrapper.text()).toContain('support@example.com');
    expect(wrapper.text()).toContain('request access to the application by');
  });
});
