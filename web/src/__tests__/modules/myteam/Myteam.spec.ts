import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';

import { getGetApiUsersResponseMock } from '@/api-access/generated/users/users.msw';
import Myteam from '@/modules/myteam/views/Myteam.vue';
import UserCard from '@/modules/myteam/components/UserCard.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaDataTableServer from '@/shared/components/UaDataTableServer.vue';
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
    [['UsersView' as const, 'UserGlobalSearch' as const], true],
    [['UsersView' as const], false],
    [['UserGlobalSearch' as const], false],
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

  it('pages results, changes page size, and resets the page for multiple locations and search', async () => {
    const app = await createTestApp({ permissions: ['UsersView', 'UserGlobalSearch'] });
    const template = getGetApiUsersResponseMock()[0]!;
    const users = Array.from({ length: 32 }, (_, index) => ({
      ...template,
      id: `user-${index + 1}`,
      firstName: `Member${String(index + 1).padStart(2, '0')}`,
      lastName: 'Example',
      homeLocationId: (index % 3) + 1,
    }));
    const requests: URL[] = [];

    server.use(
      http.get('*/api/users/paged', ({ request }) => {
        const url = new URL(request.url);
        requests.push(url);
        const page = Number(url.searchParams.get('Page'));
        const pageSize = Number(url.searchParams.get('PageSize'));
        const locations = url.searchParams.getAll('LocationId').map(Number);
        const search = url.searchParams.get('Search')?.toLowerCase();
        const filtered = users.filter(
          (user) =>
            (!locations.length || locations.includes(user.homeLocationId)) &&
            (!search || `${user.firstName} ${user.lastName}`.toLowerCase().includes(search)),
        );
        return HttpResponse.json({
          page,
          pageSize,
          totalCount: filtered.length,
          data: filtered.slice((page - 1) * pageSize, page * pageSize),
        });
      }),
    );

    const wrapper = mount(Myteam, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    const pager = wrapper.getComponent(UaDataTableServer);
    const expectRequest = (page: string, pageSize: string, locations: string[] = [], search: string | null = null) => {
      const params = requests.at(-1)!.searchParams;
      expect(params.get('Page')).toBe(page);
      expect(params.get('PageSize')).toBe(pageSize);
      expect(params.getAll('LocationId')).toEqual(locations);
      expect(params.get('Search')).toBe(search);
    };
    const expectUsers = (first: string, absent: string, count: number) => {
      expect(wrapper.findAllComponents(UserCard)).toHaveLength(count);
      expect(wrapper.text()).toContain(first);
      expect(wrapper.text()).not.toContain(absent);
    };

    expectRequest('1', '10');
    expectUsers('Member01 Example', 'Member11 Example', 10);

    pager.vm.$emit('update:page', 2);
    await flushPromises();
    expectRequest('2', '10');
    expectUsers('Member11 Example', 'Member01 Example', 10);

    pager.vm.$emit('update:items-per-page', 25);
    await flushPromises();
    expectRequest('1', '25');
    expectUsers('Member01 Example', 'Member26 Example', 25);

    pager.vm.$emit('update:page', 2);
    await flushPromises();
    expectRequest('2', '25');
    expectUsers('Member26 Example', 'Member01 Example', 7);

    wrapper.getComponent(UaAutocomplete).vm.$emit('update:modelValue', [1, 2]);
    await flushPromises();
    expectRequest('1', '25', ['1', '2']);
    expectUsers('Member01 Example', 'Member03 Example', 22);

    pager.vm.$emit('update:items-per-page', 10);
    await flushPromises();
    pager.vm.$emit('update:page', 2);
    await flushPromises();
    expectRequest('2', '10', ['1', '2']);
    expectUsers('Member16 Example', 'Member01 Example', 10);

    const searchInput = wrapper.get('#team-search');
    await searchInput.setValue('Member02');
    await searchInput.trigger('keydown.enter');
    await flushPromises();
    expectRequest('1', '10', ['1', '2'], 'Member02');
    expectUsers('Member02 Example', 'Member16 Example', 1);
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
    const app = await createTestApp({
      featureFlags: { UserManagement: { enabled: true, userBadgeNumber: { enabled: true } } },
    });
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
