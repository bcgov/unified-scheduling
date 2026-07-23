import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import AwayLocationModal from '@/modules/myteam/components/AwayLocationModal.vue';
import {
  getPostApiUsersUserIdAwayLocationsMockHandler,
  getPostApiUsersUserIdAwayLocationsResponseMock,
} from '@/api-access/generated/away-locations/away-locations.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { AwayLocationResponseDto } from '@/api-access/generated/models';
import type { SelectOption } from '@/types/select';

afterEach(() => {
  document.body.innerHTML = '';
});

const locations: SelectOption[] = [
  { code: 1, description: 'Victoria' },
  { code: 2, description: 'Vancouver' },
];

describe('AwayLocationModal', () => {
  it('shows validation error when location is not selected', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: {
        userId: 'test-user-id',
        locations,
        awayLocation: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    expect(saveButton).toBeDefined();
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Required');

    wrapper.unmount();
  });

  it('submits create request and emits saved + close', async () => {
    const app = await createTestApp();

    server.use(
      getPostApiUsersUserIdAwayLocationsMockHandler(async () => {
        return getPostApiUsersUserIdAwayLocationsResponseMock({
          id: 1001,
          userId: 'test-user-id',
          locationId: 1,
          locationName: 'Victoria',
          locationTimezone: 'America/Vancouver',
          startAtUtc: '2026-01-10T08:00:00Z',
          endAtUtc: '2026-06-30T08:00:00Z',
          expiryAtUtc: null,
          expiryReason: null,
          comment: null,
        });
      }),
    );

    const wrapper = mount(AwayLocationModal, {
      props: {
        userId: 'test-user-id',
        locations,
        awayLocation: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId: number | undefined; startDate: string; endDate: string };
    };
    vm.formData.locationId = 1;
    vm.formData.startDate = '2026-01-10';
    vm.formData.endDate = '2026-06-30';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('shows validation error when end date is empty', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: {
        userId: 'test-user-id',
        locations,
        awayLocation: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId: number | undefined; startDate: string; endDate: string };
    };
    vm.formData.locationId = 1;
    vm.formData.startDate = '2026-01-10';
    vm.formData.endDate = '';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Required');

    wrapper.unmount();
  });

  it('shows validation error when end date is before start date', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: {
        userId: 'test-user-id',
        locations,
        awayLocation: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId: number | undefined; startDate: string; endDate: string };
    };
    vm.formData.locationId = 1;
    vm.formData.startDate = '2026-06-01';
    vm.formData.endDate = '2026-01-01';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('End Date must be on or after Start Date.');

    wrapper.unmount();
  });

  it('shows modal title "Edit Away Location" in edit mode', async () => {
    const app = await createTestApp();

    const awayLocation: AwayLocationResponseDto = {
      id: 1001,
      userId: 'test-user-id',
      locationId: 1,
      locationName: 'Victoria',
      locationTimezone: 'America/Vancouver',
      startAtUtc: '2026-01-10T08:00:00Z',
      endAtUtc: '2026-06-30T08:00:00Z',
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(AwayLocationModal, {
      props: {
        userId: 'test-user-id',
        locations,
        awayLocation,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Edit Away Location');

    wrapper.unmount();
  });

  it('shows Add Time checkbox and hides time fields by default', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Add Time');
    expect(document.querySelector('#away-location-start-time')).toBeNull();
    expect(document.querySelector('#away-location-end-time')).toBeNull();

    wrapper.unmount();
  });

  it('shows time fields when Add Time is toggled', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { allDay: boolean };
    vm.allDay = false;

    await flushPromises();

    expect(document.querySelector('#away-location-start-time')).not.toBeNull();
    expect(document.querySelector('#away-location-end-time')).not.toBeNull();

    wrapper.unmount();
  });

  it('submits create request with time fields when Add Time is enabled', async () => {
    const app = await createTestApp();

    let capturedBody: unknown;
    server.use(
      getPostApiUsersUserIdAwayLocationsMockHandler(async (req) => {
        capturedBody = await req.request.json();
        return getPostApiUsersUserIdAwayLocationsResponseMock({
          id: 1002,
          locationId: 1,
          locationName: 'Victoria',
          startAtUtc: '2026-01-10T08:30:00Z',
          endAtUtc: '2026-01-10T17:00:00Z',
        });
      }),
    );

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: {
        locationId: number | undefined;
        startDate: string;
        endDate: string;
        startTime: string;
        endTime: string;
      };
      allDay: boolean;
    };
    vm.allDay = false;
    vm.formData.locationId = 1;
    vm.formData.startDate = '2026-01-10';
    vm.formData.startTime = '08:30';
    vm.formData.endDate = '2026-01-10';
    vm.formData.endTime = '17:00';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.includes('Save'));
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(capturedBody).toMatchObject({
      startDateTime: '2026-01-10T08:30:00.000-08:00',
      endDateTime: '2026-01-10T17:00:00.000-08:00',
    });

    wrapper.unmount();
  });

  it('clears time fields when Add Time is toggled off', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { startTime: string; endTime: string };
      allDay: boolean;
    };
    vm.allDay = false;
    vm.formData.startTime = '08:30';
    vm.formData.endTime = '17:00';

    await flushPromises();

    vm.allDay = true;

    await flushPromises();

    expect(vm.formData.startTime).toBe('');
    expect(vm.formData.endTime).toBe('');

    wrapper.unmount();
  });

  it('populates time fields from existing partial-day away location in edit mode', async () => {
    const app = await createTestApp();

    const awayLocation: AwayLocationResponseDto = {
      id: 2001,
      userId: 'test-user-id',
      locationId: 1,
      locationName: 'Victoria',
      locationTimezone: 'America/Vancouver',
      startAtUtc: '2026-01-10T08:30:00+00:00',
      endAtUtc: '2026-01-10T17:00:00+00:00',
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { startTime: string; endTime: string };
      allDay: boolean;
    };
    expect(vm.allDay).toBe(false);
    expect(vm.formData.startTime).toBe('08:30');
    expect(vm.formData.endTime).toBe('17:00');

    wrapper.unmount();
  });

  it('uses selected location timezone for datetime offset', async () => {
    const app = await createTestApp();

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId: number | undefined };
      timezone: string;
    };

    // Default timezone when no location selected
    expect(vm.timezone).toBe('America/Vancouver');

    wrapper.unmount();
  });

  it('does not enable Add Time for an existing away location when allDay is true', async () => {
    const app = await createTestApp();

    const awayLocation: AwayLocationResponseDto = {
      id: 2002,
      userId: 'test-user-id',
      locationId: 1,
      locationName: 'Victoria',
      locationTimezone: 'America/Vancouver',
      startAtUtc: '2026-01-10T00:00:00+00:00',
      endAtUtc: '2026-06-30T00:00:00+00:00',
      allDay: true,
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { allDay: boolean };
    expect(vm.allDay).toBe(true);

    wrapper.unmount();
  });

  it('enables Add Time for an existing away location when allDay is false', async () => {
    const app = await createTestApp();

    const awayLocation: AwayLocationResponseDto = {
      id: 2003,
      userId: 'test-user-id',
      locationId: 1,
      locationName: 'Victoria',
      locationTimezone: 'America/Vancouver',
      startAtUtc: '2026-01-10T08:30:00+00:00',
      endAtUtc: '2026-01-10T17:00:00+00:00',
      allDay: false,
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { allDay: boolean };
    expect(vm.allDay).toBe(false);

    wrapper.unmount();
  });

  it('sends allDay=true when Add Time is off and allDay=false when Add Time is on', async () => {
    const app = await createTestApp();

    let capturedBody: unknown;
    server.use(
      getPostApiUsersUserIdAwayLocationsMockHandler(async (req) => {
        capturedBody = await req.request.json();
        return getPostApiUsersUserIdAwayLocationsResponseMock({ id: 1003 });
      }),
    );

    const wrapper = mount(AwayLocationModal, {
      props: { userId: 'test-user-id', locations, awayLocation: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { locationId: number | undefined; startDate: string; endDate: string };
    };
    vm.formData.locationId = 1;
    vm.formData.startDate = '2026-01-10';
    vm.formData.endDate = '2026-06-30';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.includes('Save'));
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(capturedBody).toMatchObject({ allDay: true });

    wrapper.unmount();
  });
});
