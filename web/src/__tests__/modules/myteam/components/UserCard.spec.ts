import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';

import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import { createTestApp } from '../../../helpers/createTestApp';

describe('UserCard', async () => {
  const app = await createTestApp();

  it('mounts and renders user details', () => {
    const user = getGetApiUsersIdResponseMock();
    const fullName = `${user.firstName} ${user.lastName}`.trim();
    const initials = `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`;

    const wrapper = mount(UserCard, {
      props: {
        user,
      },
      global: {
        plugins: [app.router, app.vuetify],
      },
    });

    expect(wrapper.text()).toContain(fullName);
    expect(wrapper.text()).toContain(initials);

    if (user.badgeNumber) {
      expect(wrapper.text()).toContain(user.badgeNumber);
    }
  });
});
