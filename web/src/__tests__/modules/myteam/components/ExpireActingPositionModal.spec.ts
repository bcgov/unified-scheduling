import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import ExpireActingPositionModal from '@/modules/myteam/components/ExpireActingPositionModal.vue';
import {
  getPostApiUsersUserIdActingPositionsExpireMockHandler,
  getPostApiUsersUserIdActingPositionsExpireResponseMock,
} from '@/api-access/generated/acting-positions/acting-positions.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { ActingPositionResponseDto } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
});

const position: ActingPositionResponseDto = {
  id: 1001,
  userId: 'test-user-id',
  positionTypeCode: 'SGT',
  positionTypeDescription: 'Sergeant',
  startAtUtc: '2026-01-10T08:00:00Z',
  expiryAtUtc: null,
  expiryReason: null,
  comment: null,
};

describe('ExpireActingPositionModal', () => {
  it('shows validation error when no reason is selected', async () => {
    const app = await createTestApp();

    const wrapper = mount(ExpireActingPositionModal, {
      props: { userId: 'test-user-id', position },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const expireButton = Array.from(document.querySelectorAll('button')).find((b) => b.textContent?.includes('Expire'));
    expect(expireButton).toBeDefined();
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Reason for expiry is required.');

    wrapper.unmount();
  });

  it('submits expire request and emits expired + close', async () => {
    const app = await createTestApp();

    server.use(
      getPostApiUsersUserIdActingPositionsExpireMockHandler(() =>
        getPostApiUsersUserIdActingPositionsExpireResponseMock({
          ...position,
          expiryAtUtc: new Date().toISOString(),
          expiryReason: 'ENTRYERR',
        }),
      ),
    );

    const wrapper = mount(ExpireActingPositionModal, {
      props: { userId: 'test-user-id', position },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { formData: { expiryReason: string } };
    vm.formData.expiryReason = 'ENTRYERR';

    await flushPromises();

    const expireButton = Array.from(document.querySelectorAll('button')).find(
      (b) => b.textContent?.includes('Expire') && !b.textContent?.includes('Acting'),
    );
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('expired')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('displays position description in confirm message', async () => {
    const app = await createTestApp();

    const wrapper = mount(ExpireActingPositionModal, {
      props: { userId: 'test-user-id', position },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Sergeant');

    wrapper.unmount();
  });
});
