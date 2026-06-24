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

    const vm = wrapper.vm as unknown as { formData: { positionTypeCode: string; startDate: string; endDate: string } };
    vm.formData.positionTypeCode = 'SGT';
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
});
