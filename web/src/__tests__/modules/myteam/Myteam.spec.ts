import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';

import { getGetApiUsersMockHandler, getGetApiUsersResponseMock } from '@/api-access/generated/users/users.msw';
import Myteam from '@/modules/myteam/views/Myteam.vue';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import { createTestApp } from '../../helpers/createTestApp';
import { server } from '../../mocks/server';
import { useLocationsStore } from '@/stores/LocationsStore';

describe('Myteam', () => {
  it('fetches users and renders user cards', async () => {
    const app = await createTestApp();

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
        plugins: app.mountPlugins,
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
    const app = await createTestApp();

    server.use(
      http.get('*/api/users', () => {
        return HttpResponse.json({ message: 'Failed to load users' }, { status: 500, statusText: 'Server Error' });
      }),
    );

    const wrapper = mount(Myteam, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(wrapper.text()).toContain('Error:');
  });

  it('clears the search and reloads users when the clear button is clicked', async () => {
    const app = await createTestApp();
    const capturedSearchTerms: Array<string | null> = [];

    server.use(
      getGetApiUsersMockHandler((info) => {
        capturedSearchTerms.push(new URL(info.request.url).searchParams.get('Search'));
        return getGetApiUsersResponseMock();
      }),
    );

    const wrapper = mount(Myteam, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    const searchField = wrapper.findComponent({ name: 'VTextField' });
    expect(wrapper.get('label[for="team-search"]').text()).toBe('Search');
    expect(searchField.props('prependInnerIcon')).toBe('mdi-magnify');
    expect(searchField.props('density')).toBe('comfortable');
    expect(searchField.props('variant')).toBe('outlined');

    const searchInput = wrapper.get('#team-search');
    await searchInput.setValue('Doe');
    await searchInput.trigger('keydown.enter');
    await flushPromises();

    expect(capturedSearchTerms.at(-1)).toBe('Doe');

    await wrapper.get('.v-field__clearable [role="button"]').trigger('click');
    await flushPromises();

    expect(capturedSearchTerms.at(-1)).toBeNull();
  });

  it('sends LocationId query param when a location is selected', async () => {
    const app = await createTestApp();
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(42);

    let capturedLocationId: string | null = null;

    server.use(
      getGetApiUsersMockHandler((info) => {
        const url = new URL(info.request.url);
        capturedLocationId = url.searchParams.get('LocationId');
        return getGetApiUsersResponseMock();
      }),
    );

    mount(Myteam, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(capturedLocationId).toBe('42');
  });

  it('omits LocationId query param when no location is selected', async () => {
    const app = await createTestApp();

    let capturedUrl: URL | null = null;

    server.use(
      getGetApiUsersMockHandler((info) => {
        capturedUrl = new URL(info.request.url);
        return getGetApiUsersResponseMock();
      }),
    );

    mount(Myteam, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(capturedUrl).not.toBeNull();

    expect(capturedUrl!.searchParams.has('LocationId')).toBe(false);
  });
});
