import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import App from '@/App.vue';
import { Permissions, type LoanOutCalendarUser, type UserResponse } from '@/api-access/generated/models';
import type { CalendarConflict, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import CalendarSchedulingDistributionModal from '@/modules/scheduling/CalendarSchedulingDistributionModal.vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import { useNotificationsStore } from '@/stores/notifications';
import { useUsersStore } from '@/stores/Users';
import { useScheduleDistributionStore } from '@/modules/scheduling/calendarSchedulingDistributionStore';
import { server } from '../../mocks/server';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('CalendarSchedulingDistributionModal', () => {
  it.each(['schedule', 'assignments'] as const)(
    'offers and distributes unique home and borrowed recipients displayed in the %s view',
    async (view) => {
      const home = createRecipient('home');
      const undisplayedHome = createRecipient('undisplayed');
      const borrowed = createBorrowedRecipient('borrowed');
      const disabled = { ...borrowed, id: 'disabled', isEnabled: false };
      const elsewhere = createBorrowedRecipient('elsewhere', 8);
      const expired = createBorrowedRecipient('expired');
      expired.loanOutMetaData![0]!.endAtUtc = '2026-08-24T07:00:00Z';
      const future = createBorrowedRecipient('future');
      future.loanOutMetaData![0]!.startAtUtc = '2026-08-25T07:00:00Z';
      const noEmail = { ...borrowed, id: 'no-email', firstName: 'Borrowed', lastName: 'No Email', email: ' ' };
      const requests: unknown[] = [];
      server.use(
        http.get('*/api/users', ({ request }) => {
          expect(new URL(request.url).searchParams.get('LocationId')).toBe('7');
          return HttpResponse.json([home, undisplayedHome]);
        }),
        http.post('*/api/loan-out/scheduling/users', async ({ request }) => {
          expect(await request.json()).toEqual({
            locationId: 7,
            startDate: '2026-08-24',
            endDate: '2026-08-24',
            timeZoneId: 'America/Vancouver',
          });
          return HttpResponse.json([
            borrowed,
            { ...borrowed, loanOutMetaData: [...borrowed.loanOutMetaData!, ...borrowed.loanOutMetaData!] },
            { ...home, loanOutMetaData: borrowed.loanOutMetaData },
            disabled,
            elsewhere,
            expired,
            future,
            noEmail,
          ]);
        }),
        http.post('*/api/scheduling/distribute', async ({ request }) => {
          requests.push(await request.json());
          return HttpResponse.json({ recipientCount: 2 });
        }),
      );
      const wrapper = await mountRecipientModal({ view });
      try {
        await flushPromises();
        const autocomplete = wrapper.getComponent(UaAutocomplete);
        expect(
          autocomplete
            .props('items')
            .map((item) => item.code)
            .sort(),
        ).toEqual(['home', 'borrowed', 'no-email', 'undisplayed'].sort());
        expect(autocomplete.props('modelValue')).toEqual(['home', 'borrowed']);
        const send = wrapper.findAllComponents(UaBtn).find((button) => button.text() === 'Send')!;
        autocomplete.vm.$emit('update:modelValue', ['home', 'borrowed', 'no-email']);
        await wrapper.vm.$nextTick();
        expect(document.body.textContent).toContain('Borrowed No Email does not have an email address');
        expect(send.element).toHaveProperty('disabled', true);
        send.vm.$emit('click');
        await flushPromises();
        expect(requests).toEqual([]);

        autocomplete.vm.$emit('update:modelValue', ['home', 'borrowed']);
        await wrapper.vm.$nextTick();
        send.vm.$emit('click');
        await flushPromises();
        expect(requests).toEqual([
          expect.objectContaining({
            context: expect.objectContaining({ view }),
            recipientUserIds: ['home', 'borrowed'],
          }),
        ]);
      } finally {
        wrapper.unmount();
      }
    },
  );

  it.each([
    undefined,
    { featureFlags: { LoanOut: { enabled: false } }, permissions: [Permissions.LoanOutView] },
    { featureFlags: { LoanOut: { enabled: true } }, permissions: [] },
  ] satisfies (CalendarRuntimeContext | undefined)[])(
    'preserves home-only recipients without calling loan-out when unavailable (%j)',
    async (runtimeContext) => {
      const loanOutRequest = vi.fn(() => HttpResponse.json([createBorrowedRecipient('borrowed')]));
      server.use(
        http.get('*/api/users', () => HttpResponse.json([createRecipient('home')])),
        http.post('*/api/loan-out/scheduling/users', loanOutRequest),
      );
      const wrapper = await mountRecipientModal({ runtimeContext });
      try {
        await flushPromises();
        expect(loanOutRequest).not.toHaveBeenCalled();
        expect(wrapper.getComponent(UaAutocomplete).props('modelValue')).toEqual(['home']);
        expect(wrapper.getComponent(UaAutocomplete).props('items')).toEqual([
          expect.objectContaining({ code: 'home' }),
        ]);
      } finally {
        wrapper.unmount();
      }
    },
  );

  it.each(['home', 'borrowed'])(
    'blocks sending after a failed %s recipient lookup, including after printing',
    async (failedLookup) => {
      server.use(
        http.get('*/api/users', () =>
          failedLookup === 'home'
            ? new HttpResponse(null, { status: 500 })
            : HttpResponse.json([createRecipient('home')]),
        ),
        http.post('*/api/loan-out/scheduling/users', () =>
          failedLookup === 'borrowed'
            ? new HttpResponse(null, { status: 500 })
            : HttpResponse.json([createBorrowedRecipient('borrowed')]),
        ),
      );
      const openWindow = vi.spyOn(window, 'open').mockReturnValue({ opener: window } as Window);
      const wrapper = await mountRecipientModal();
      try {
        await flushPromises();
        const autocomplete = wrapper.getComponent(UaAutocomplete);
        expect(autocomplete.props('modelValue')).toEqual([]);
        expect(document.body.textContent).toContain('Failed to load schedule recipients.');
        autocomplete.vm.$emit('update:modelValue', ['home']);
        await wrapper.vm.$nextTick();
        wrapper
          .findAllComponents(UaBtn)
          .find((button) => button.text() === 'Print')!
          .vm.$emit('click');
        await wrapper.vm.$nextTick();
        expect(openWindow).toHaveBeenCalledOnce();
        const send = wrapper.findAllComponents(UaBtn).find((button) => button.text() === 'Send')!;
        expect(send.element).toHaveProperty('disabled', true);
        send.vm.$emit('click');
        await flushPromises();
        expect(wrapper.emitted('close')).toBeUndefined();
        expect(document.body.textContent).toContain('Failed to load schedule recipients.');
      } finally {
        wrapper.unmount();
        openWindow.mockRestore();
      }
    },
  );

  it.each(['success', 'failure'])(
    'loads recipients concurrently and ignores stale loan-out %s after date and location changes',
    async (staleResult) => {
      const homeResponse = deferredResponse();
      const oldLoanOutResponse = deferredResponse();
      let homeRequests = 0;
      let loanOutRequests = 0;
      server.use(
        http.get('*/api/users', async () => {
          homeRequests += 1;
          if (homeRequests === 1) await homeResponse.promise;
          return HttpResponse.json([createRecipient('home')]);
        }),
        http.post('*/api/loan-out/scheduling/users', async ({ request }) => {
          loanOutRequests += 1;
          const body = (await request.json()) as { locationId: number; startDate: string; endDate: string };
          if (body.startDate === '2026-08-24') {
            await oldLoanOutResponse.promise;
            return staleResult === 'failure'
              ? new HttpResponse(null, { status: 500 })
              : HttpResponse.json([createBorrowedRecipient('borrowed')]);
          }
          expect(body).toMatchObject({ locationId: 8, startDate: '2026-08-25', endDate: '2026-08-25' });
          const current = createBorrowedRecipient('current', 8);
          current.loanOutMetaData![0]!.startAtUtc = '2026-08-25T16:00:00Z';
          current.loanOutMetaData![0]!.endAtUtc = '2026-08-26T00:00:00Z';
          return HttpResponse.json([current]);
        }),
      );
      const wrapper = await mountRecipientModal();
      try {
        await flushPromises();
        expect(homeRequests).toBe(1);
        expect(loanOutRequests).toBe(1);
        expect(wrapper.getComponent(UaAutocomplete).props('modelValue')).toEqual([]);
        await wrapper.setProps({ locationId: 8, startDate: '2026-08-25', endDate: '2026-08-26' });
        await flushPromises();
        const autocomplete = wrapper.getComponent(UaAutocomplete);
        expect(autocomplete.props('modelValue')).toEqual(['home']);
        autocomplete.vm.$emit('update:modelValue', ['current']);
        await wrapper.vm.$nextTick();
        homeResponse.resolve();
        oldLoanOutResponse.resolve();
        await flushPromises();
        expect(autocomplete.props('modelValue')).toEqual(['current']);
        expect(
          autocomplete
            .props('items')
            .map((item) => item.code)
            .sort(),
        ).toEqual(['current', 'home']);
        expect(document.body.textContent).not.toContain('Failed to load schedule recipients.');
      } finally {
        homeResponse.resolve();
        oldLoanOutResponse.resolve();
        wrapper.unmount();
      }
    },
  );

  it('cancels the pending borrowed-recipient lookup on unmount without distributing', async () => {
    const pending = deferredResponse();
    const signals: AbortSignal[] = [];
    server.use(
      http.get('*/api/users', async () => {
        await pending.promise;
        return HttpResponse.json([createRecipient('home')]);
      }),
      http.post('*/api/loan-out/scheduling/users', async ({ request }) => {
        signals.push(request.signal);
        await pending.promise;
        return HttpResponse.json([createBorrowedRecipient('borrowed')]);
      }),
    );
    const wrapper = await mountRecipientModal();
    await flushPromises();
    expect(signals).toHaveLength(1);
    wrapper.unmount();
    await flushPromises();
    expect(signals.every((signal) => signal.aborted)).toBe(true);
    pending.resolve();
    await flushPromises();
    expect(wrapper.emitted('close')).toBeUndefined();
  });

  it('reuses a pending home-user lookup when only the dates change and retains the latest selection', async () => {
    const homeResponse = deferredResponse();
    let homeRequests = 0;
    const ranges: unknown[] = [];
    server.use(
      http.get('*/api/users', async () => {
        homeRequests += 1;
        await homeResponse.promise;
        return HttpResponse.json([createRecipient('home')]);
      }),
      http.post('*/api/loan-out/scheduling/users', async ({ request }) => {
        ranges.push(await request.json());
        const borrowed = createBorrowedRecipient('borrowed');
        borrowed.loanOutMetaData![0]!.endAtUtc = '2026-08-27T00:00:00Z';
        return HttpResponse.json([borrowed]);
      }),
    );
    const wrapper = await mountRecipientModal();
    try {
      await flushPromises();
      await wrapper.setProps({ startDate: '2026-08-25', endDate: '2026-08-26' });
      await flushPromises();
      expect(homeRequests).toBe(1);
      expect(ranges).toEqual([
        expect.objectContaining({ startDate: '2026-08-24', endDate: '2026-08-24' }),
        expect.objectContaining({ startDate: '2026-08-25', endDate: '2026-08-25' }),
      ]);
      homeResponse.resolve();
      await flushPromises();
      const autocomplete = wrapper.getComponent(UaAutocomplete);
      expect(autocomplete.props('modelValue')).toEqual(['home', 'borrowed']);
      expect(document.body.textContent).not.toContain('Failed to load schedule recipients.');
      autocomplete.vm.$emit('update:modelValue', ['borrowed']);
      await wrapper.setProps({ model: { ...wrapper.props('model') } });
      await flushPromises();
      expect(autocomplete.props('modelValue')).toEqual(['borrowed']);
      expect(ranges).toHaveLength(2);
    } finally {
      homeResponse.resolve();
      wrapper.unmount();
    }
  });

  it('offers enabled recipients and rejects selections that lose eligibility before sending', async () => {
    const enabled: UserResponse = {
      id: 'd20d7f24-b50e-42f7-aef8-f45e36918bae',
      idirName: 'enabled',
      idirId: null,
      isEnabled: true,
      firstName: 'Enabled',
      lastName: 'Recipient',
      email: 'enabled@example.com',
      employeeNumber: '',
      gender: 'Other',
      rank: null,
      badgeNumber: null,
      homeLocationId: 7,
      lastLogin: null,
    };
    const disabled = {
      ...enabled,
      id: '79d8d8b1-9e53-43d2-8ede-c149b1351dce',
      isEnabled: false,
      firstName: 'Disabled',
      email: 'disabled@example.com',
    };
    const noEmail = {
      ...enabled,
      id: 'bbef24da-a49d-48b7-9f83-65e39753e476',
      firstName: 'No',
      lastName: 'Email',
      email: '',
    };
    let users = [enabled, disabled, noEmail];
    const requests: unknown[] = [];
    server.use(
      http.get('*/api/users', () => HttpResponse.json(users)),
      http.post('*/api/scheduling/distribute', async ({ request }) => {
        requests.push(await request.json());
        return HttpResponse.json({ recipientCount: 1 });
      }),
    );
    const app = await createTestApp({ loadConfig: false });
    app.router.addRoute({ path: '/distribution-test', component: { template: '<div />' } });
    await app.router.push('/distribution-test');
    await app.router.isReady();
    const wrapper = mount(CalendarSchedulingDistributionModal, {
      props: {
        locationId: 7,
        startDate: '2026-08-24',
        endDate: '2026-08-25',
        period: 'day',
        view: 'schedule',
        model: {
          timeZone: 'America/Vancouver',
          days: [],
          cells: [],
          primaryColumn: {
            label: 'TEAM',
            resources: [enabled, disabled].map((user) => ({
              id: user.id,
              type: 'user',
              title: user.firstName!,
            })),
          },
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    try {
      await flushPromises();
      const autocomplete = wrapper.getComponent(UaAutocomplete);
      expect(autocomplete.props('modelValue')).toEqual([enabled.id]);
      expect(autocomplete.props('items')).toEqual([
        expect.objectContaining({ code: enabled.id }),
        expect.objectContaining({ code: noEmail.id }),
      ]);
      const send = wrapper.findAllComponents(UaBtn).find((button) => button.text() === 'Send')!;
      autocomplete.vm.$emit('update:modelValue', [enabled.id, noEmail.id]);
      await wrapper.vm.$nextTick();
      expect(document.body.textContent).toContain('No Email does not have an email address');
      expect(send.element).toHaveProperty('disabled', true);
      send.vm.$emit('click');
      await flushPromises();
      expect(requests).toEqual([]);

      autocomplete.vm.$emit('update:modelValue', [enabled.id]);
      users = [{ ...enabled, isEnabled: false }, disabled, noEmail];
      const usersStore = useUsersStore(app.pinia);
      usersStore.invalidateLocation(7);
      await usersStore.ensureUsersForLocation(7);
      await flushPromises();
      expect(autocomplete.props('items')).toEqual([expect.objectContaining({ code: noEmail.id })]);
      send.vm.$emit('click');
      await flushPromises();
      expect(document.body.textContent).toContain(
        'Some selected recipients are no longer eligible. Remove them before sending.',
      );
      expect(autocomplete.props('modelValue')).toEqual([enabled.id]);
      expect(requests).toEqual([]);
      expect(wrapper.emitted('close')).toBeUndefined();

      users = [enabled, disabled, noEmail];
      usersStore.invalidateLocation(7);
      await usersStore.ensureUsersForLocation(7);
      await flushPromises();
      send.vm.$emit('click');
      await flushPromises();
      expect(requests).toEqual([expect.objectContaining({ recipientUserIds: [enabled.id] })]);
      expect(wrapper.emitted('close')).toEqual([[]]);
    } finally {
      wrapper.unmount();
    }
  });

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

function deferredResponse() {
  let resolve!: () => void;
  const promise = new Promise<void>((complete) => {
    resolve = complete;
  });
  return { promise, resolve };
}

function createRecipient(id: string): UserResponse {
  return {
    id,
    idirName: id,
    isEnabled: true,
    firstName: id,
    lastName: 'Recipient',
    email: `${id}@example.com`,
    employeeNumber: '',
    gender: 'Other',
    homeLocationId: 7,
  };
}

function createBorrowedRecipient(id: string, locationId = 7): LoanOutCalendarUser {
  return {
    ...createRecipient(id),
    firstName: id,
    lastName: 'Recipient',
    email: `${id}@example.com`,
    employeeNumber: '',
    homeLocationId: 99,
    loanOutMetaData: [
      { loanOutLocationId: locationId, startAtUtc: '2026-08-24T16:00:00Z', endAtUtc: '2026-08-25T00:00:00Z' },
    ],
  };
}

async function mountRecipientModal(
  overrides: {
    view?: 'schedule' | 'assignments';
    runtimeContext?: CalendarRuntimeContext;
  } = {},
) {
  const app = await createTestApp({ loadConfig: false });
  app.router.addRoute({ path: '/distribution-test', component: { template: '<div />' } });
  await app.router.push('/distribution-test');
  await app.router.isReady();
  const displayedUsers = ['home', 'borrowed', 'disabled', 'elsewhere', 'expired', 'future'].map((id) => ({
    id,
    type: 'user',
    title: id,
  }));
  return mount(CalendarSchedulingDistributionModal, {
    props: {
      locationId: 7,
      startDate: '2026-08-24',
      endDate: '2026-08-25',
      period: 'day',
      view: 'schedule',
      runtimeContext: { featureFlags: { LoanOut: { enabled: true } }, permissions: [Permissions.LoanOutView] },
      ...overrides,
      model: {
        timeZone: 'America/Vancouver',
        days: [],
        cells: [],
        primaryColumn: { label: 'TEAM', resources: overrides.view === 'assignments' ? [] : displayedUsers },
        sidePanel: overrides.view === 'assignments' ? { label: 'TEAM', items: displayedUsers } : undefined,
      },
    },
    global: { plugins: app.mountPlugins },
    attachTo: document.body,
  });
}

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
    overrideNote: isOverridden ? 'Approved' : null,
    createdById: null,
    createdOn: null,
    updatedById: null,
    updatedOn: null,
  };
}
