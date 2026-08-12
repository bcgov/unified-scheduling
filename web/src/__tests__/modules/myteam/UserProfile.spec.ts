import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { getGetApiUsersIdMockHandler, getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import UserProfile from '@/modules/myteam/views/UserProfile.vue';
import { server } from '../../mocks/server';
import { createTestApp } from '../../helpers/createTestApp';

describe('UserProfile', () => {
  it('fetches the user by prop userId and renders profile data with badge when feature flag is enabled', async () => {
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: true, required: false } } },
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
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: false, required: false } } },
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
