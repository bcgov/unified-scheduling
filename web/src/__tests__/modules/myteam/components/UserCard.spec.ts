import { afterEach, describe, it, expect, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { VImg } from 'vuetify/components';
import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import { createTestApp } from '../../../helpers/createTestApp';

describe('UserCard', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it.each([
    ['2026-11-19T12:34:56+00:00', '2026-11-19T12:34:56+00:00'],
    [null, 'photo-user'],
  ])('renders the prefixed photo URL and preserves cache-busting for %s', async (lastPhotoUpdate, cacheKey) => {
    vi.stubEnv('BASE_URL', '/sheriff-scheduling/');
    const app = await createTestApp();
    const user = getGetApiUsersIdResponseMock({
      id: 'photo-user',
      photoUrl: '/api/users/photo-user/photo',
      lastPhotoUpdate,
    });

    const wrapper = mount(UserCard, { props: { user }, global: { plugins: app.mountPlugins } });
    await flushPromises();

    expect(wrapper.findComponent(VImg).props('src')).toBe(
      `/sheriff-scheduling/api/users/photo-user/photo?t=${cacheKey}`,
    );
    wrapper.unmount();
  });

  it('keeps user details visible when the position lookup fails', async () => {
    server.use(http.get('*/api/lookup/PositionTypes', () => new HttpResponse(null, { status: 500 })));
    const app = await createTestApp();
    const user = getGetApiUsersIdResponseMock({ firstName: 'Jane', lastName: 'Doe', rank: 'Sergeant' });
    const wrapper = mount(UserCard, { props: { user }, global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('Jane Doe');
    expect(wrapper.text()).toContain('-');
    wrapper.unmount();
  });
  it('mounts and renders user details including badge when feature flag is enabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: false, userBadgeNumber: { enabled: true } } },
    });

    const user = getGetApiUsersIdResponseMock({ photoUrl: null });
    const fullName = `${user.firstName} ${user.lastName}`.trim();
    const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`;

    const wrapper = mount(UserCard, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).toContain(fullName);
    expect(wrapper.text()).toContain(initials);

    if (user.badgeNumber) {
      expect(wrapper.text()).toContain(user.badgeNumber);
    }
  });

  it('hides badge number when feature flag is disabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: false, userBadgeNumber: { enabled: false } } },
    });

    const user = getGetApiUsersIdResponseMock({
      badgeNumber: 'ABC123',
    });

    const wrapper = mount(UserCard, {
      props: {
        user,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    expect(wrapper.text()).not.toContain('ABC123');
  });
});
