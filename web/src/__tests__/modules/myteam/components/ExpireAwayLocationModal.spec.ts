import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import ExpireAwayLocationModal from '@/modules/myteam/components/ExpireAwayLocationModal.vue';
import {
  getPostApiUsersUserIdAwayLocationsExpireMockHandler,
  getPostApiUsersUserIdAwayLocationsExpireResponseMock,
} from '@/api-access/generated/away-locations/away-locations.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { AwayLocationResponseDto } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
});

const awayLocation: AwayLocationResponseDto = {
  id: 1001,
  eventId: 1,
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

describe('ExpireAwayLocationModal', () => {
  it('shows validation error when no reason is selected', async () => {
    const app = await createTestApp();

    const wrapper = mount(ExpireAwayLocationModal, {
      props: { userId: 'test-user-id', awayLocation },
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
      getPostApiUsersUserIdAwayLocationsExpireMockHandler(() =>
        getPostApiUsersUserIdAwayLocationsExpireResponseMock({
          ...awayLocation,
          expiryAtUtc: new Date().toISOString(),
          expiryReason: 'ENTRYERR',
        }),
      ),
    );

    const wrapper = mount(ExpireAwayLocationModal, {
      props: { userId: 'test-user-id', awayLocation },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { formData: { expiryReason: string } };
    vm.formData.expiryReason = 'ENTRYERR';

    await flushPromises();

    const expireButton = Array.from(document.querySelectorAll('button')).find(
      (b) => b.textContent?.includes('Expire') && !b.textContent?.includes('Away'),
    );
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(wrapper.emitted('expired')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('displays location name in confirm message', async () => {
    const app = await createTestApp();

    const wrapper = mount(ExpireAwayLocationModal, {
      props: { userId: 'test-user-id', awayLocation },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Victoria');

    wrapper.unmount();
  });
});
