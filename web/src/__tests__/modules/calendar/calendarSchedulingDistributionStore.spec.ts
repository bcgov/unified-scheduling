import { describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import App from '@/App.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { useScheduleDistributionStore } from '@/modules/scheduling/calendarSchedulingDistributionStore';
import { useNotificationsStore } from '@/stores/notifications';

describe('schedule distribution completion', () => {
  it.each(['http-error', 'network-error', 'invalid-result'] as const)(
    'transitions the pending notification to a persistent failure on %s without retrying',
    async (failure) => {
      let finish!: () => void;
      const responseReady = new Promise<void>((resolve) => {
        finish = resolve;
      });
      let requestCount = 0;
      server.use(
        http.post('*/api/scheduling/distribute', async () => {
          requestCount += 1;
          await responseReady;
          if (failure === 'network-error') return HttpResponse.error();
          if (failure === 'invalid-result') return HttpResponse.json({});
          return HttpResponse.json({ title: 'PDF rendering failed.' }, { status: 502 });
        }),
      );
      const app = await createTestApp({ loadConfig: false });
      app.router.addRoute({ path: '/notification-test', component: { template: '<div />' } });
      await app.router.push('/notification-test');
      const shell = mount(App, {
        global: { plugins: app.mountPlugins, stubs: { Appbar: true } },
        attachTo: document.body,
      });
      const store = useScheduleDistributionStore(app.pinia);
      const notifications = useNotificationsStore(app.pinia);
      const unhandled = vi.fn();
      window.addEventListener('unhandledrejection', unhandled);
      try {
        expect(
          store.send({
            context: {
              view: 'assignments',
              locationId: 7,
              startDate: '2026-08-24',
              endDate: '2026-08-25',
              period: 'day',
            },
            recipientUserIds: ['user-1'],
          }),
        ).toBe(true);
        expect(store.isPending).toBe(true);
        expect(notifications.message).toBe('Sending distribution…');
        expect(notifications.pending).toBe(true);
        await flushPromises();
        expect(document.body.textContent).toContain('Sending distribution…');
        finish();
        await flushPromises();
        expect(store.isPending).toBe(false);
        expect(notifications.message).toBe('Distribution failed. Please try again.');
        expect(notifications.color).toBe('error');
        expect(notifications.pending).toBe(false);
        expect(notifications.visible).toBe(true);
        expect(notifications.timeout).toBe(-1);
        expect(document.body.textContent).toContain('Distribution failed. Please try again.');
        expect(document.body.textContent).not.toContain('Sending distribution…');
        expect(unhandled).not.toHaveBeenCalled();
        expect(requestCount).toBe(1);
        const dismiss = Array.from(document.querySelectorAll('button')).find(
          (button) => button.textContent?.trim() === 'Dismiss',
        );
        expect(dismiss).toBeDefined();
        dismiss?.click();
        await shell.vm.$nextTick();
        expect(notifications.visible).toBe(false);
      } finally {
        finish();
        window.removeEventListener('unhandledrejection', unhandled);
        shell.unmount();
      }
    },
  );
});
