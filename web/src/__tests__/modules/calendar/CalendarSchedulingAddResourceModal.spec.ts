import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createPinia } from 'pinia';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import LuxonAdapter from '@date-io/luxon';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useCalendarStore } from '@/modules/calendar/calendarStore';

function createModalTestApp() {
  const pinia = createPinia();
  const vuetify = createVuetify({
    components,
    directives,
    date: { adapter: LuxonAdapter },
  });

  return { pinia, mountPlugins: [pinia, vuetify] };
}

type ShiftApiRequest = {
  execute?: () => Promise<unknown>;
};

type ShiftApiMock = {
  entries?: unknown[];
  postEntry?: (body: unknown, options?: unknown) => ShiftApiRequest;
  postEntryPublish?: (id: unknown, options?: unknown) => ShiftApiRequest;
  postSeries?: (body: unknown, options?: unknown) => ShiftApiRequest;
  postSeriesPublish?: (id: unknown, options?: unknown) => ShiftApiRequest;
  series?: unknown[];
};

async function executeRequest(request: ShiftApiRequest | undefined) {
  await request?.execute?.();
  return request;
}

function buildShiftApiMock(api: ShiftApiMock) {
  return {
    loadShiftEntries: vi.fn().mockResolvedValue({
      data: { value: api.entries ?? [] },
      error: { value: null },
    }),
    loadShiftSeriesList: vi.fn().mockResolvedValue({
      data: { value: api.series ?? [] },
      error: { value: null },
    }),
    createShiftEntry: (body: unknown) => executeRequest(api.postEntry?.(body, { options: { immediate: false } })),
    createShiftSeries: (body: unknown) => executeRequest(api.postSeries?.(body, { options: { immediate: false } })),
    publishShiftEntry: (id: unknown) => executeRequest(api.postEntryPublish?.(id, { options: { immediate: false } })),
    publishShiftSeries: (id: unknown) => executeRequest(api.postSeriesPublish?.(id, { options: { immediate: false } })),
  };
}

const buildUsersModuleMock = () => ({
  getApiUsers: vi.fn().mockReturnValue({
    data: {
      value: [
        {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          idirName: 'aalpha',
          idirId: null,
          isEnabled: true,
          firstName: 'Alex',
          lastName: 'Alpha',
          email: 'alex.alpha@example.com',
          gender: 'Male',
          rank: null,
          badgeNumber: null,
          homeLocationId: 12,
          lastLogin: null,
        },
      ],
    },
    error: { value: null },
    execute: vi.fn().mockResolvedValue(undefined),
  }),
});

const buildUsersModuleMockForUser = (userId: string, firstName = 'Alex', lastName = 'Alpha') => ({
  getApiUsers: vi.fn().mockReturnValue({
    data: {
      value: [
        {
          id: userId,
          idirName: `${firstName.toLowerCase()}${lastName.toLowerCase()}`,
          idirId: null,
          isEnabled: true,
          firstName,
          lastName,
          email: `${firstName.toLowerCase()}.${lastName.toLowerCase()}@example.com`,
          gender: 'Male',
          rank: null,
          badgeNumber: null,
          homeLocationId: 12,
          lastLogin: null,
        },
      ],
    },
    error: { value: null },
    execute: vi.fn().mockResolvedValue(undefined),
  }),
});

describe('CalendarSchedulingAddResourceModal', () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it.each([
    {
      label: 'entry',
      entries: [
        {
          id: 41,
          locationId: 12,
          startAtUtc: '2026-08-24T16:00:00Z',
          endAtUtc: '2026-08-25T00:00:00Z',
        },
      ],
      series: [],
      expectedWarning: true,
    },
    {
      label: 'series',
      entries: [],
      series: [
        {
          id: 42,
          locationId: 12,
          startAtUtc: '2026-08-24T16:00:00+00:00',
          endAtUtc: '2026-08-25T00:00:00+00:00',
        },
      ],
      expectedWarning: true,
    },
    {
      label: 'entry at another location',
      entries: [
        {
          id: 43,
          locationId: 99,
          startAtUtc: '2026-08-24T16:00:00Z',
          endAtUtc: '2026-08-25T00:00:00Z',
        },
      ],
      series: [],
      expectedWarning: false,
    },
  ])('shows the duplicate warning for a matching $label', async ({ entries, series, expectedWarning }) => {
    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({
        entries,
        series,
        postEntry: vi.fn(),
        postEntryPublish: vi.fn(),
        postSeries: vi.fn(),
        postSeriesPublish: vi.fn(),
      }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');
    const app = createModalTestApp();
    useLocationsStore(app.pinia).setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: { initialDate: '2026-08-24', timeZone: 'America/Vancouver' },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent?.includes('Shift with selected Date/Time already exists.')).toBe(expectedWarning);

    wrapper.unmount();
  });

  it('posts a shift entry, refreshes the calendar, and closes the modal', async () => {
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 321 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    const postSeries = vi.fn();
    const postEntryPublish = vi.fn();
    const postSeriesPublish = vi.fn();

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry, postEntryPublish, postSeries, postSeriesPublish }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    const calendarStore = useCalendarStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        cancel: 'yes' | 'no';
        date?: string;
        publish: 'yes' | 'no';
      };
    };

    expect(vm.formData.cancel).toBe('no');
    vm.formData.date = '2026-07-02';
    vm.formData.publish = 'no';

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(postEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        locationId: 12,
        userIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postEntryExecute).toHaveBeenCalled();
    expect(postEntryPublish).not.toHaveBeenCalled();
    expect(calendarStore.refreshNonce).toBe(1);
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('posts a shift entry from the create action without a preselected resource', async () => {
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 322 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({
        postEntry,
        postEntryPublish: vi.fn(),
        postSeries: vi.fn(),
        postSeriesPublish: vi.fn(),
      }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');
    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: { initialDate: '2026-08-24', timeZone: 'America/Vancouver' },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();
    locationsStore.setSelectedLocationId(12);
    await flushPromises();
    const vm = wrapper.vm as unknown as {
      handleSave: () => Promise<void>;
      formErrors: Record<string, string>;
      formData: { locationId?: number | null };
      apiError: string;
    };
    expect(vm.formData.locationId).toBe(12);
    await vm.handleSave();

    expect(vm.formErrors).toEqual({});
    expect(vm.apiError).toBe('');
    expect(postEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'New shift',
        locationId: 12,
        userIds: [],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postEntryExecute).toHaveBeenCalled();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('posts a recurring shift series and publishes it when requested', async () => {
    const postSeriesExecute = vi.fn().mockResolvedValue(undefined);
    const publishSeriesExecute = vi.fn().mockResolvedValue(undefined);

    const postSeries = vi.fn().mockReturnValue({
      data: { value: { id: 654 } },
      error: { value: null },
      execute: postSeriesExecute,
    });
    const postSeriesPublish = vi.fn().mockReturnValue({
      data: { value: { id: 654 } },
      error: { value: null },
      execute: publishSeriesExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry: vi.fn(), postEntryPublish: vi.fn(), postSeries, postSeriesPublish }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
        repeatMode: 'never' | 'custom';
        recurrenceRule?: string | null;
        publish: 'yes' | 'no';
      };
    };

    vm.formData.date = '2026-07-02';
    vm.formData.repeatMode = 'custom';
    vm.formData.recurrenceRule = 'FREQ=WEEKLY;COUNT=2';
    vm.formData.publish = 'yes';

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(postSeries).toHaveBeenCalledWith(
      expect.objectContaining({
        locationId: 12,
        recurrenceRule: 'FREQ=WEEKLY;COUNT=2',
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postSeriesExecute).toHaveBeenCalled();
    expect(postSeriesPublish).toHaveBeenCalledWith(654, expect.objectContaining({ options: { immediate: false } }));
    expect(publishSeriesExecute).toHaveBeenCalled();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('shows inline field errors when save is clicked with invalid values', async () => {
    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({
        postEntry: vi.fn(),
        postEntryPublish: vi.fn(),
        postSeries: vi.fn(),
        postSeriesPublish: vi.fn(),
      }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
        startTime?: string;
        endTime?: string;
      };
    };

    vm.formData.date = '';
    vm.formData.startTime = '';
    vm.formData.endTime = '';

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    const content = document.body.textContent ?? '';
    expect(content).toContain('Could not save the shift. Check the highlighted fields.');
    expect(content).toContain('Required');
    expect((content.match(/Required/g) ?? []).length).toBeGreaterThanOrEqual(3);

    wrapper.unmount();
  });

  it('normalizes displayed time labels back to model values before save', async () => {
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 321 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry, postEntryPublish: vi.fn(), postSeries: vi.fn(), postSeriesPublish: vi.fn() }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
        startTime?: string;
        endTime?: string;
      };
    };

    vm.formData.date = '2026-07-02';
    vm.formData.startTime = '9:00am';
    vm.formData.endTime = '5:00pm';

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(vm.formData.startTime).toBe('09:00');
    expect(vm.formData.endTime).toBe('17:00');
    expect(postEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        locationId: 12,
        userIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postEntryExecute).toHaveBeenCalled();
    expect(document.body.textContent ?? '').not.toContain('Invalid start time.');
    expect(document.body.textContent ?? '').not.toContain('Invalid end time.');

    wrapper.unmount();
  });

  it('allows saving with no selected employees', async () => {
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 321 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry, postEntryPublish: vi.fn(), postSeries: vi.fn(), postSeriesPublish: vi.fn() }),
    );
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
        publish: 'yes' | 'no';
        userIds?: string[];
      };
    };

    vm.formData.date = '2026-07-02';
    vm.formData.publish = 'no';
    vm.formData.userIds = [];

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(postEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        locationId: 12,
        userIds: [],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postEntryExecute).toHaveBeenCalled();
    expect(document.body.textContent ?? '').not.toContain('Required');

    wrapper.unmount();
  });

  it('accepts system-style guid user ids during validation and save', async () => {
    const systemUserId = '00000000-0000-0000-0000-000000000001';
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 321 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry, postEntryPublish: vi.fn(), postSeries: vi.fn(), postSeriesPublish: vi.fn() }),
    );
    vi.doMock('@/api-access/generated/users/users', () =>
      buildUsersModuleMockForUser(systemUserId, 'System', 'System'),
    );

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        resource: {
          id: systemUserId,
          type: 'user',
          title: 'System System',
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
        userIds?: string[];
      };
    };

    vm.formData.date = '2026-07-02';

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(postEntry).toHaveBeenCalledWith(
      expect.objectContaining({
        locationId: 12,
        userIds: [systemUserId],
      }),
      expect.objectContaining({ options: { immediate: false } }),
    );
    expect(postEntryExecute).toHaveBeenCalled();
    expect(document.body.textContent ?? '').not.toContain('Invalid UUID');

    wrapper.unmount();
  });

  it('keeps a label option for an initially selected assignment while async options load', async () => {
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: vi.fn(),
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        initialAssignmentEntryId: 251,
        initialAssignmentEvents: [
          {
            id: 'scheduling.assignment-entry.251',
            type: 'scheduling.assignment',
            sourceModule: 'scheduling',
            title: 'Yellow Assignment',
            start: '2026-07-13T16:00:00Z',
            end: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            metadata: {
              assignmentEntryId: '251',
            },
          } as never,
        ],
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        assignmentEntryIds?: number[];
      };
      mergedAssignmentEntryOptions: Array<{ code: number | string; description: string }>;
    };

    expect(vm.formData.assignmentEntryIds).toEqual([251]);
    expect(vm.mergedAssignmentEntryOptions).toContainEqual({
      code: 251,
      description: 'Yellow Assignment (9:00 AM - 5:00 PM)',
    });

    wrapper.unmount();
  });

  it('initializes assignment entry links with assignedUserIds for the selected user', async () => {
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: vi.fn(),
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        initialAssignmentEntryId: 251,
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        assignmentEntryLinks?: Array<{
          assignmentEntryId?: number;
          assignedUserIds?: string[];
          userIds?: string[];
        }>;
      };
    };

    expect(vm.formData.assignmentEntryLinks).toEqual([
      {
        assignmentEntryId: 251,
        assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
      },
    ]);
    expect(vm.formData.assignmentEntryLinks?.[0]).not.toHaveProperty('userIds');

    wrapper.unmount();
  });

  it('sends assignedUserIds when saving a shift created from assignment and user context', async () => {
    const postEntryExecute = vi.fn().mockResolvedValue(undefined);
    const postEntry = vi.fn().mockReturnValue({
      data: { value: { id: 321 } },
      error: { value: null },
      execute: postEntryExecute,
    });

    vi.doMock('@/modules/scheduling/calendarSchedulingShiftApi', () =>
      buildShiftApiMock({ postEntry, postEntryPublish: vi.fn(), postSeries: vi.fn(), postSeriesPublish: vi.fn() }),
    );
    const syncAssignmentEntryLinks = vi.fn().mockResolvedValue(undefined);
    vi.doMock('@/modules/scheduling/calendarSchedulingShiftAssignmentApi', () => ({
      syncAssignmentEntryLinks,
      syncAssignmentSeriesLinks: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        initialAssignmentEntryId: 251,
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        date?: string;
      };
      handleSave: () => Promise<void>;
      formErrors: Record<string, string>;
      apiError: string;
    };
    vm.formData.date = '2026-07-12';
    await vm.handleSave();

    expect(vm.formErrors).toEqual({});
    expect(vm.apiError).toBe('');
    expect(postEntry).toHaveBeenCalled();
    expect(syncAssignmentEntryLinks).toHaveBeenCalledWith(
      251,
      [
        {
          shiftEntryId: 321,
          assignedUserIds: ['3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62'],
        },
      ],
      [],
    );
    expect(postEntryExecute).toHaveBeenCalled();

    wrapper.unmount();
  });

  it('uses the initial assignment event label when only an assignment id is preselected', async () => {
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: vi.fn(),
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = createModalTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);

    const wrapper = mount(CalendarSchedulingAddResourceModal, {
      props: {
        initialAssignmentEntryId: 259,
        initialAssignmentEvents: [
          {
            id: 'scheduling.assignment-entry.259',
            type: 'scheduling.assignment',
            sourceModule: 'scheduling',
            title: 'Yellow Assignment',
            start: '2026-07-13T16:00:00Z',
            end: '2026-07-14T00:00:00Z',
            timeZoneId: 'America/Vancouver',
            metadata: {
              assignmentEntryId: '259',
            },
          } as never,
        ],
        resource: {
          id: '3d6f0a75-0a77-4dd9-9f5a-f4d0a0bc4f62',
          type: 'user',
          title: 'Alex Alpha',
        },
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      mergedAssignmentEntryOptions: Array<{ code: number | string; description: string }>;
    };

    expect(vm.mergedAssignmentEntryOptions.find((option) => option.code === 259)?.description).toBe(
      'Yellow Assignment (9:00 AM - 5:00 PM)',
    );

    wrapper.unmount();
  });
});
