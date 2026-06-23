import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import ActingPositionModal from '@/modules/myteam/components/ActingPositionModal.vue';
import {
  getPostApiUsersUserIdActingPositionsMockHandler,
  getPostApiUsersUserIdActingPositionsResponseMock,
} from '@/api-access/generated/acting-positions/acting-positions.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { ActingPositionResponseDto } from '@/api-access/acting-positions';
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

  it('shows validation error when expiry date is before effective date', async () => {
    const app = await createTestApp();

    const wrapper = mount(ActingPositionModal, {
      props: {
        userId: 'test-user-id',
        positionTypes,
        position: {
          id: 1001,
          userId: 'test-user-id',
          positionTypeCode: 'SGT',
          positionTypeDescription: 'Sergeant',
          effectiveDate: '2026-06-01T00:00:00Z',
          expiryDate: '2026-01-01T00:00:00Z',
          expiryReason: null,
          comment: null,
        } satisfies ActingPositionResponseDto,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Expiry date cannot be earlier than effective date.');

    wrapper.unmount();
  });

  it('submits create request and emits saved + close', async () => {
    const app = await createTestApp();

    let requestBody: Record<string, unknown> | null = null;

    server.use(
      getPostApiUsersUserIdActingPositionsMockHandler(async (info) => {
        requestBody = (await info.request.json()) as Record<string, unknown>;
        return getPostApiUsersUserIdActingPositionsResponseMock({
          id: 1001,
          userId: 'test-user-id',
          positionTypeCode: 'SGT',
          positionTypeDescription: 'Sergeant',
          effectiveDate: '2026-01-10T08:00:00Z',
          expiryDate: null,
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
    const vm = wrapper.vm as unknown as { formData: { positionTypeCode: string; effectiveDate: string } };
    vm.formData.positionTypeCode = 'SGT';
    vm.formData.effectiveDate = '2026-01-10';

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

  it('shows modal title "Edit Acting Position" in edit mode', async () => {
    const app = await createTestApp();

    const position: ActingPositionResponseDto = {
      id: 1001,
      userId: 'test-user-id',
      positionTypeCode: 'SGT',
      positionTypeDescription: 'Sergeant',
      effectiveDate: '2026-01-10T08:00:00Z',
      expiryDate: null,
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
