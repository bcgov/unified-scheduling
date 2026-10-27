import { describe, expect, it, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import App from '@/App.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { useNotificationsStore } from '@/stores/notifications';

describe('global notifications', () => {
  it('keeps pending feedback visible and automatically dismisses its success replacement', async () => {
    const app = await createTestApp({ loadConfig: false });
    app.router.addRoute({ path: '/notification-test', component: { template: '<div />' } });
    await app.router.push('/notification-test');
    const shell = mount(App, {
      global: { plugins: app.mountPlugins, stubs: { Appbar: true } },
    });
    const notifications = useNotificationsStore(app.pinia);
    vi.useFakeTimers();
    try {
      notifications.show('Sending distribution…', 'info', true);
      await shell.vm.$nextTick();
      await vi.advanceTimersByTimeAsync(10_000);
      expect(notifications.visible).toBe(true);
      notifications.dismiss();
      expect(notifications.visible).toBe(true);
      notifications.show('Distribution submitted successfully.', 'success');
      await shell.vm.$nextTick();
      await vi.advanceTimersByTimeAsync(5000);
      expect(notifications.visible).toBe(false);
    } finally {
      shell.unmount();
      vi.useRealTimers();
    }
  });
});
