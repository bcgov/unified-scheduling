import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';

import { getGetApiUsersIdMockHandler, getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import UserProfile from '@/modules/myteam/UserProfile.vue';
import { server } from '../../mocks/server';
import { createTestApp } from '../../helpers/createTestApp';

describe('UserProfile', async () => {
  const app = await createTestApp();

  it('fetches the user by prop userId and renders profile data', async () => {
    const userId = 'test-user-id';
    const user = getGetApiUsersIdResponseMock();

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
        plugins: [app.router, app.vuetify],
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
});
