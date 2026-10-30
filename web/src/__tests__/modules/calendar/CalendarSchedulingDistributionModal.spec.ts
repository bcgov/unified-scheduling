import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import App from '@/App.vue';
import type { UserResponse } from '@/api-access/generated/models';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import CalendarSchedulingDistributionModal from '@/modules/scheduling/CalendarSchedulingDistributionModal.vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { useNotificationsStore } from '@/stores/notifications';
import { useScheduleDistributionStore } from '@/modules/scheduling/calendarSchedulingDistributionStore';
import { server } from '../../mocks/server';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('CalendarSchedulingDistributionModal', () => {
  it('starts once, closes the modal, and completes globally after unmount and navigation', async () => {
    let finish!: () => void;
    const responseReady = new Promise<void>((resolve) => {
      finish = resolve;
    });
    let requestCount = 0;
    const recipient: UserResponse = {
      id: 'd20d7f24-b50e-42f7-aef8-f45e36918bae',
      idirName: 'alex.morgan',
      idirId: null,
      isEnabled: true,
      firstName: 'Alex',
      lastName: 'Morgan',
      email: 'alex.morgan@example.com',
      employeeNumber: '1001',
      gender: 'Male',
      rank: null,
      badgeNumber: null,
      homeLocationId: 7,
      lastLogin: null,
    };
    server.use(
      http.get('*/api/users', ({ request }) => {
        expect(new URL(request.url).searchParams.get('LocationId')).toBe('7');
        return HttpResponse.json([recipient]);
      }),
      http.post('*/api/scheduling/distribute', async ({ request }) => {
        requestCount += 1;
        expect(await request.json()).toEqual({
          context: {
            view: 'schedule',
            locationId: 7,
            startDate: '2026-08-24',
            endDate: '2026-08-25',
            period: 'day',
            viewportWidth: window.innerWidth,
            viewportHeight: window.innerHeight,
          },
          recipientUserIds: [recipient.id],
        });
        await responseReady;
        return HttpResponse.json({ recipientCount: 1 });
      }),
    );

    const app = await createTestApp({ loadConfig: false });
    const { pinia, router } = app;
    router.addRoute({ path: '/distribution-test', component: { template: '<div />' } });
    router.addRoute({ path: '/other-test', component: { template: '<div>Other page</div>' } });
    await router.push('/distribution-test');
    await router.isReady();
    const notifications = useNotificationsStore(pinia);
    const distribution = useScheduleDistributionStore(pinia);
    const shell = mount(App, {
      global: { plugins: app.mountPlugins, stubs: { Appbar: true } },
      attachTo: document.body,
    });
    const unresolvedConflict = createConflict('conflict-1', false);
    const overriddenConflict = createConflict('conflict-2', true);
    const model: CalendarMatrixViewModel = {
      timeZone: 'America/Vancouver',
      payload: {
        conflictsAvailable: true,
        conflicts: [unresolvedConflict, overriddenConflict],
        events: [
          {
            id: 'draft-shift',
            type: 'scheduling.shift',
            sourceModule: 'scheduling',
            title: 'Draft shift',
            start: '2026-08-24T09:00:00-07:00',
            statusTypeCode: 'Draft',
            locationId: 7,
            metadata: {},
          },
        ],
      },
      days: [],
      primaryColumn: { label: 'TEAM', resources: [{ id: recipient.id, type: 'user', title: 'Alex Morgan' }] },
      cells: [
        {
          resourceId: recipient.id,
          date: '2026-08-24',
          headers: [{ text: 'Shift', conflicts: [{ conflict: unresolvedConflict, currentEventId: 1 }] }],
          groups: [
            {
              id: 'assignments',
              events: [
                {
                  event: {
                    id: 'active-assignment',
                    type: 'scheduling.assignment',
                    sourceModule: 'scheduling',
                    title: 'Assignment',
                    start: '2026-08-24T10:00:00-07:00',
                  },
                  conflicts: [
                    { conflict: unresolvedConflict, currentEventId: 1 },
                    { conflict: overriddenConflict, currentEventId: 1 },
                  ],
                },
              ],
            },
          ],
          payload: {
            shiftEvents: [
              {
                id: 'draft-shift',
                type: 'scheduling.shift',
                sourceModule: 'scheduling',
                title: 'Draft shift',
                start: '2026-08-24T09:00:00-07:00',
                statusTypeCode: 'Draft',
                locationId: 7,
              },
            ],
          },
        },
      ],
    };

    const wrapper = mount(CalendarSchedulingDistributionModal, {
      props: {
        locationId: 7,
        startDate: '2026-08-24',
        endDate: '2026-08-25',
        period: 'day',
        view: 'schedule',
        model,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const autocomplete = wrapper.getComponent(UaAutocomplete);
    expect(autocomplete.props('modelValue')).toEqual([recipient.id]);
    autocomplete.vm.$emit('update:search', 'does not match');
    await wrapper.vm.$nextTick();
    expect(autocomplete.props('items')).toEqual([
      expect.objectContaining({ code: recipient.id, description: 'Alex Morgan (alex.morgan@example.com)' }),
    ]);
    autocomplete.vm.$emit('update:modelValue', []);
    await wrapper.vm.$nextTick();
    expect(autocomplete.props('modelValue')).toEqual([]);
    autocomplete.vm.$emit('update:modelValue', [recipient.id]);
    await wrapper.vm.$nextTick();

    const draftAlert = wrapper
      .findAllComponents(UaAlert)
      .find((alert) => alert.text().includes('This schedule contains unpublished changes.'));
    expect(draftAlert?.props('type')).toBe('warning');
    expect(document.body.textContent).toContain('This schedule contains unpublished changes.');
    expect(document.body.textContent).toContain('1 unresolved conflict and 1 overridden conflict');

    const sendButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Send',
    );
    expect(sendButton).toBeDefined();
    sendButton?.dispatchEvent(new Event('click', { bubbles: true }));
    sendButton?.dispatchEvent(new Event('click', { bubbles: true }));
    // Observe the first Vue render, before the nextTick continuation initiates the request.
    await Promise.resolve();
    expect(sendButton?.textContent).toContain('Starting distribution…');
    expect(sendButton).toHaveProperty('disabled', true);
    expect(sendButton?.querySelector('.v-progress-circular')).not.toBeNull();
    await flushPromises();

    expect(requestCount).toBe(1);
    expect(wrapper.emitted('close')).toEqual([[]]);
    expect(distribution.isPending).toBe(true);
    expect(notifications.message).toBe('Sending distribution…');
    expect(notifications.timeout).toBe(-1);
    notifications.dismiss();
    expect(notifications.visible).toBe(true);
    // Even a reopened modal cannot bypass the store's pending guard.
    expect(
      distribution.send({
        context: {
          view: 'schedule',
          locationId: 7,
          startDate: '2026-08-24',
          endDate: '2026-08-25',
          period: 'day',
        },
        recipientUserIds: [recipient.id],
      }),
    ).toBe(false);
    wrapper.unmount();
    await router.push('/other-test');
    await flushPromises();
    expect(document.body.textContent).toContain('Sending distribution…');
    expect(document.body.textContent).toContain('Other page');

    finish();
    await flushPromises();
    expect(distribution.isPending).toBe(false);
    expect(notifications.message).toBe('Distribution submitted successfully.');
    expect(notifications.color).toBe('success');
    expect(notifications.timeout).toBe(5000);
    expect(document.body.textContent).toContain('Distribution submitted successfully.');
    expect(document.body.textContent).not.toContain('Sending distribution…');
    expect(requestCount).toBe(1);
    shell.unmount();
  });

  it('prevents recipients without email and reports unavailable conflict information', async () => {
    const recipient: UserResponse = {
      id: 'bbef24da-a49d-48b7-9f83-65e39753e476',
      idirName: 'no.email',
      idirId: null,
      isEnabled: true,
      firstName: 'No',
      lastName: 'Email',
      email: ' ',
      employeeNumber: '',
      gender: 'Other',
      rank: null,
      badgeNumber: null,
      homeLocationId: 7,
      lastLogin: null,
    };
    server.use(http.get('*/api/users', () => HttpResponse.json([recipient])));
    const app = await createTestApp({ loadConfig: false });
    const { router } = app;
    router.addRoute({ path: '/distribution-test', component: { template: '<div />' } });
    await router.push('/distribution-test');
    await router.isReady();
    const wrapper = mount(CalendarSchedulingDistributionModal, {
      props: {
        locationId: 7,
        startDate: '2026-08-24',
        endDate: '2026-08-25',
        period: 'day',
        view: 'schedule',
        model: {
          timeZone: 'America/Vancouver',
          payload: { conflictsAvailable: false, conflicts: [], events: [] },
          days: [],
          primaryColumn: { label: 'TEAM', resources: [{ id: recipient.id, type: 'user', title: 'No Email' }] },
          cells: [],
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent).toContain('No Email does not have an email address');
    expect(document.body.textContent).toContain('Conflict information is unavailable');
    const sendButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Send',
    );
    expect(sendButton).toHaveProperty('disabled', true);

    // Exercise the handler guard as well as the disabled button.
    const send = wrapper.findAllComponents(UaBtn).find((button) => button.text() === 'Send')!;
    send.vm.$emit('click');
    await flushPromises();
    expect(wrapper.emitted('close')).toBeUndefined();
    expect(document.body.textContent).toContain('Remove recipients without an email address before sending.');
    expect(useScheduleDistributionStore(app.pinia).isPending).toBe(false);
    expect(useNotificationsStore(app.pinia).visible).toBe(false);

    wrapper.getComponent(UaAutocomplete).vm.$emit('update:modelValue', []);
    await wrapper.vm.$nextTick();
    send.vm.$emit('click');
    await flushPromises();
    expect(document.body.textContent).toContain('Select at least one recipient.');
    expect(wrapper.emitted('close')).toBeUndefined();

    wrapper.unmount();
  });
});

function createConflict(id: string, isOverridden: boolean): CalendarConflict {
  const entry = {
    eventId: '1',
    sourceModule: 'scheduling',
    title: 'Shift',
    start: '2026-08-24T16:00:00Z',
    end: '2026-08-25T00:00:00Z',
    sourceEntityId: 1,
    timeZoneId: 'America/Vancouver',
  };
  return {
    id,
    entry,
    overlaps: { ...entry, eventId: '2', title: 'Assignment', sourceEntityId: 2 },
    resourceId: 'd20d7f24-b50e-42f7-aef8-f45e36918bae',
    overlapStart: '2026-08-24T17:00:00Z',
    overlapEnd: '2026-08-24T18:00:00Z',
    isOverridden,
    reportOnEntry: true,
    reportOnOverlaps: true,
    overrideNote: isOverridden ? 'Approved' : null,
    createdById: null,
    createdOn: null,
    updatedById: null,
    updatedOn: null,
  };
}
