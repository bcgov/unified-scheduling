import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { VImg } from 'vuetify/components';

import { getGetApiUsersIdMockHandler, getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import UserProfile from '@/modules/myteam/views/UserProfile.vue';
import { server } from '../../mocks/server';
import { createTestApp } from '../../helpers/createTestApp';

describe('UserProfile', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('renders the prefixed photo URL with the encoded cache-busting timestamp', async () => {
    vi.stubEnv('BASE_URL', '/sheriff-scheduling/');
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true } },
    });
    const user = getGetApiUsersIdResponseMock({
      id: 'photo-user',
      photoUrl: '/api/users/photo-user/photo',
      lastPhotoUpdate: '2026-11-19T12:34:56+00:00',
    });
    server.use(getGetApiUsersIdMockHandler(user));

    const wrapper = mount(UserProfile, {
      props: { userId: 'photo-user' },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.findComponent(VImg).props('src')).toBe(
      '/sheriff-scheduling/api/users/photo-user/photo?v=2026-11-19T12%3A34%3A56%2B00%3A00',
    );
    wrapper.unmount();
  });

  it('fetches the user by prop userId and renders profile data with badge when feature flag is enabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: true } } },
    });

    const userId = 'test-user-id';
    const user = getGetApiUsersIdResponseMock({ photoUrl: null });

    server.use(
      getGetApiUsersIdMockHandler((info) => {
        expect(info.params.id).toBe(userId);
        return user;
      }),
    );

    const wrapper = mount(UserProfile, {
      props: {
        userId,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    const fullName = `${user.firstName} ${user.lastName}`.trim();
    const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`;

    expect(wrapper.text()).toContain('Profile');
    expect(wrapper.text()).not.toContain('Loading ...');
    expect(wrapper.text()).toContain(fullName);
    expect(wrapper.text()).toContain(initials);

    if (user.badgeNumber) {
      expect(wrapper.text()).toContain(user.badgeNumber);
    }
  });

  it('hides badge number when feature flag is disabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: false } } },
    });

    const userId = 'test-user-id';
    const user = getGetApiUsersIdResponseMock({
      badgeNumber: 'ABC123',
    });

    server.use(
      getGetApiUsersIdMockHandler((info) => {
        expect(info.params.id).toBe(userId);
        return user;
      }),
    );

    const wrapper = mount(UserProfile, {
      props: {
        userId,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(wrapper.text()).not.toContain('ABC123');
  });

  it('shows training tab when training module and permission are enabled', async () => {
    const app = await createTestApp({
      featureFlags: {
        Training: { enabled: true },
        UserManagement: { enabled: true },
      },
      permissions: ['UserTrainingsView' as never],
    });

    const userId = 'test-user-id';
    const user = getGetApiUsersIdResponseMock();

    server.use(getGetApiUsersIdMockHandler(user));

    const wrapper = mount(UserProfile, {
      props: {
        userId,
      },
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('Training');
  });
});
