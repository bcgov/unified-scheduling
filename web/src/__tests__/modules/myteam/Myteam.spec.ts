import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';

import { getGetApiUsersResponseMock } from '@/api-access/generated/users/users.msw';
import Myteam from '@/modules/myteam/views/Myteam.vue';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import { createTestApp } from '../../helpers/createTestApp';
import { server } from '../../mocks/server';
import { useLocationsStore } from '@/stores/LocationsStore';

const getPagedUsersHandler = (
  onRequest: (info: { request: Request }) => ReturnType<typeof getGetApiUsersResponseMock> | void,
) =>
  http.get('*/api/users/paged', (info) => {
    const users = onRequest(info) ?? getGetApiUsersResponseMock();
    return HttpResponse.json({ page: 1, pageSize: 10, totalCount: users.length, data: users });
  });

describe('Myteam', () => {
  it.each([
    [['UserGlobalSearch' as const], true],
    [[], false],
  ])('location filter visibility with permissions %j is %s', async (permissions, visible) => {
    const app = await createTestApp({ permissions });
    server.use(getPagedUsersHandler(() => getGetApiUsersResponseMock()));

    const wrapper = mount(Myteam, { global: { plugins: app.mountPlugins } });
    await flushPromises();

    expect(wrapper.find('#team-location').exists()).toBe(visible);
  });

  it('fetches users and renders user cards', async () => {
    const app = await createTestApp();

    const users = getGetApiUsersResponseMock();

    server.use(
      getPagedUsersHandler((info) => {
        const url = new URL(info.request.url);

        expect(url.searchParams.get('IsEnabled')).toBe('true');
        expect(url.searchParams.get('Page')).toBe('1');
        expect(url.searchParams.get('PageSize')).toBe('10');

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
      http.get('*/api/users/paged', () => {
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
      getPagedUsersHandler((info) => {
        capturedSearchTerms.push(new URL(info.request.url).searchParams.get('Search'));
      }),
    );

    const wrapper = mount(Myteam, {
      global: {
        plugins: app.mountPlugins,
      },
    });

    await flushPromises();

    expect(wrapper.get('label[for="team-search"]').text()).toBe('Search by name or badge number');

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
      getPagedUsersHandler((info) => {
        const url = new URL(info.request.url);
        capturedLocationId = url.searchParams.get('LocationId');
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
      getPagedUsersHandler((info) => {
        capturedUrl = new URL(info.request.url);
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
