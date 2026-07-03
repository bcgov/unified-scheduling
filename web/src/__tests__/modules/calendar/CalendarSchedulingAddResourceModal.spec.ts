import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { createTestApp } from '@/__tests__/helpers/createTestApp';

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

    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: postEntry,
      postApiSchedulingShiftEntriesIdPublish: postEntryPublish,
      postApiSchedulingShiftSeries: postSeries,
      postApiSchedulingShiftSeriesIdPublish: postSeriesPublish,
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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

    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: vi.fn(),
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: postSeries,
      postApiSchedulingShiftSeriesIdPublish: postSeriesPublish,
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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
    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: vi.fn(),
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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

    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: postEntry,
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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

    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: postEntry,
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', buildUsersModuleMock);

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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

    vi.doMock('@/api-access/generated/shift/shift', () => ({
      postApiSchedulingShiftEntries: postEntry,
      postApiSchedulingShiftEntriesIdPublish: vi.fn(),
      postApiSchedulingShiftSeries: vi.fn(),
      postApiSchedulingShiftSeriesIdPublish: vi.fn(),
    }));
    vi.doMock('@/api-access/generated/users/users', () =>
      buildUsersModuleMockForUser(systemUserId, 'System', 'System'),
    );

    const { default: CalendarSchedulingAddResourceModal } =
      await import('@/modules/scheduling/CalendarSchedulingAddResourceModal.vue');

    const app = await createTestApp({ loadConfig: false });
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
});
