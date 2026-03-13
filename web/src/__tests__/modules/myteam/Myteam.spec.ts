import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';

import { getGetApiUsersMockHandler, getGetApiUsersResponseMock } from '@/api-access/generated/users/users.msw';
import Myteam from '@/modules/myteam/Myteam.vue';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import { createTestApp } from '../../helpers/createTestApp';
import { server } from '../../mocks/server';

describe('Myteam', async () => {
  const app = await createTestApp();

  it('fetches users and renders user cards', async () => {
    const users = getGetApiUsersResponseMock();

    server.use(
      getGetApiUsersMockHandler((info) => {
        const url = new URL(info.request.url);

        expect(url.searchParams.get('IsEnabled')).toBe('true');

        return users;
      }),
    );

    const wrapper = mount(Myteam, {
      global: {
        plugins: [app.router, app.vuetify],
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('My Team');
    expect(wrapper.text()).not.toContain('Loading ...');
    expect(wrapper.findAllComponents(UserCard)).toHaveLength(users.length);

    for (const user of users) {
      const fullName = `${user.firstName} ${user.lastName}`.trim();

      expect(wrapper.text()).toContain(fullName);
    }
  });

  it('renders an error state when the users request fails', async () => {
    server.use(
      http.get('*/api/users', () => {
        return HttpResponse.json({ message: 'Failed to load users' }, { status: 500, statusText: 'Server Error' });
      }),
    );

    const wrapper = mount(Myteam, {
      global: {
        plugins: [app.router, app.vuetify],
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('Error:');
  });
});