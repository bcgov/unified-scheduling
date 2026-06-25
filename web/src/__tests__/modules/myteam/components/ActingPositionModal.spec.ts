import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import ActingPositionModal from '@/modules/myteam/components/ActingPositionModal.vue';
import {
  getPostApiUsersUserIdActingPositionsMockHandler,
  getPostApiUsersUserIdActingPositionsResponseMock,
} from '@/api-access/generated/acting-positions/acting-positions.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { ActingPositionResponseDto } from '@/api-access/generated/models';
import type { SelectOption } from '@/types/select';

afterEach(() => {
  document.body.innerHTML = '';
});

const positionTypes: SelectOption[] = [
  { code: 'SGT', description: 'Sergeant' },
  { code: 'CPL', description: 'Corporal' },
];

describe('ActingPositionModal', () => {
  it('shows validation error when position type is not selected', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position: null,
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
      getPostApiUsersUserIdActingPositionsMockHandler(async () => {
        return getPostApiUsersUserIdActingPositionsResponseMock({
          id: 1001,
          userId: 'test-user-id',
          positionTypeCode: 'SGT',
          positionTypeDescription: 'Sergeant',
          startAtUtc: '2026-01-10T08:00:00Z',
          endAtUtc: '2026-06-30T08:00:00Z',
          expiryAtUtc: null,
          expiryReason: null,
          comment: null,
        });
      }),
    );

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    // Select position type via select component
    const vm = wrapper.vm as unknown as { formData: { positionTypeCode: string; startDate: string; endDate: string } };
    vm.formData.positionTypeCode = 'SGT';
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

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { formData: { positionTypeCode: string; startDate: string; endDate: string } };
    vm.formData.positionTypeCode = 'SGT';
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

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm2 = wrapper.vm as unknown as { formData: { positionTypeCode: string; startDate: string; endDate: string } };
    vm2.formData.positionTypeCode = 'SGT';
    vm2.formData.startDate = '2026-06-01';
    vm2.formData.endDate = '2026-01-01';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('End Date must be on or after Start Date.');

    wrapper.unmount();
  });

  it('shows modal title "Edit Acting Position" in edit mode', async () => {
    const app = await createTestApp();

    const position: ActingPositionResponseDto = {
      id: 1001,
      userId: 'test-user-id',
      positionTypeCode: 'SGT',
      positionTypeDescription: 'Sergeant',
      startAtUtc: '2026-01-10T08:00:00Z',
      endAtUtc: '2026-06-30T08:00:00Z',
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Edit Acting Position');

    wrapper.unmount();
  });

  it('shows Add Time checkbox and hides time fields by default', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Add Time');
    expect(document.querySelector('#acting-position-start-time')).toBeNull();
    expect(document.querySelector('#acting-position-end-time')).toBeNull();

    wrapper.unmount();
  });

  it('shows time fields when Add Time is toggled', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { addTime: boolean };
    vm.addTime = true;

    await flushPromises();

    expect(document.querySelector('#acting-position-start-time')).not.toBeNull();
    expect(document.querySelector('#acting-position-end-time')).not.toBeNull();

    wrapper.unmount();
  });

  it('submits create request with time fields when Add Time is enabled', async () => {
    const app = await createTestApp();

    let capturedBody: unknown;
    server.use(
      getPostApiUsersUserIdActingPositionsMockHandler(async (req) => {
        capturedBody = await req.request.json();
        return getPostApiUsersUserIdActingPositionsResponseMock({
          id: 1002,
          positionTypeCode: 'CPL',
          startAtUtc: '2026-01-10T08:30:00Z',
          endAtUtc: '2026-01-10T17:00:00Z',
        });
      }),
    );

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { positionTypeCode: string; startDate: string; endDate: string; startTime: string; endTime: string };
      addTime: boolean;
    };
    vm.addTime = true;
    vm.formData.positionTypeCode = 'CPL';
    vm.formData.startDate = '2026-01-10';
    vm.formData.startTime = '08:30';
    vm.formData.endDate = '2026-01-10';
    vm.formData.endTime = '17:00';

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.includes('Save'));
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(capturedBody).toMatchObject({ startDateTime: '2026-01-10T08:30:00.000-08:00', endDateTime: '2026-01-10T17:00:00.000-08:00' });

    wrapper.unmount();
  });

  it('shows validation error when only startTime is provided without endTime', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { positionTypeCode: string; startDate: string; endDate: string; startTime: string; endTime: string };
      addTime: boolean;
    };
    vm.addTime = true;
    vm.formData.positionTypeCode = 'SGT';
    vm.formData.startDate = '2026-01-10';
    vm.formData.startTime = '08:30';
    vm.formData.endDate = '2026-01-10';
    // endTime intentionally left empty

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.includes('Save'));
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Required');

    wrapper.unmount();
  });

  it('clears time fields when Add Time is toggled off', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { startTime: string; endTime: string };
      addTime: boolean;
    };
    vm.addTime = true;
    vm.formData.startTime = '08:30';
    vm.formData.endTime = '17:00';

    await flushPromises();

    vm.addTime = false;

    await flushPromises();

    expect(vm.formData.startTime).toBe('');
    expect(vm.formData.endTime).toBe('');

    wrapper.unmount();
  });

  it('populates time fields from existing partial-day position in edit mode', async () => {
    const app = await createTestApp();

    const position: ActingPositionResponseDto = {
      id: 2001,
      userId: 'test-user-id',
      positionTypeCode: 'SGT',
      positionTypeDescription: 'Sergeant',
      startAtUtc: '2026-01-10T08:30:00+00:00',
      endAtUtc: '2026-01-10T17:00:00+00:00',
      expiryAtUtc: null,
      expiryReason: null,
      comment: null,
    };

    const wrapper = mount(ActingPositionModal, {
      props: { userId: 'test-user-id', positionTypes, position },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: { startTime: string; endTime: string };
      addTime: boolean;
    };
    expect(vm.addTime).toBe(true);
    expect(vm.formData.startTime).toBe('08:30');
    expect(vm.formData.endTime).toBe('17:00');

    wrapper.unmount();
  });
});
