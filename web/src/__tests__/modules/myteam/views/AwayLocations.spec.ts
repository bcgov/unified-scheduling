import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import AwayLocations from '@/modules/myteam/views/AwayLocations.vue';
import { getGetApiUsersUserIdAwayLocationsMockHandler } from '@/api-access/generated/away-locations/away-locations.msw';
import { Permissions, type AwayLocationResponseDto, type UserResponse } from '@/api-access/generated/models';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';

afterEach(() => {
  document.body.innerHTML = '';
});

const user: UserResponse = {
  id: 'test-user-id',
  idirName: 'testuser',
  idirId: null,
  isEnabled: true,
  firstName: 'Test',
  lastName: 'User',
  email: 'test@example.com',
  gender: 'Male' as UserResponse['gender'],
  rank: null,
  badgeNumber: null,
  homeLocationId: 1,
  lastLogin: null,
};

const awayLocationFixtures: AwayLocationResponseDto[] = [
  {
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
    comment: 'Training visit',
    timezone: 'America/Vancouver',
  },
  {
    id: 1002,
    eventId: 2,
    userId: 'test-user-id',
    locationId: 2,
    locationName: 'Vancouver',
    locationTimezone: 'America/Vancouver',
    startAtUtc: '2026-03-01T08:00:00Z',
    endAtUtc: '2026-09-01T08:00:00Z',
    expiryAtUtc: null,
    expiryReason: null,
    comment: null,
    timezone: 'America/Vancouver',
  },
];

describe('AwayLocations', () => {
  it('renders the heading', async () => {
    const app = await createTestApp();
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.text()).toContain('Away Locations');
  });

  it('shows away locations in the table when data is loaded', async () => {
    const app = await createTestApp();
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.text()).toContain('Victoria');
    expect(wrapper.text()).toContain('Vancouver');
  });

  it('shows empty state when there are no away locations', async () => {
    const app = await createTestApp();
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => []));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.text()).toContain('No away locations');
  });

  it('shows error alert when the API fails', async () => {
    const app = await createTestApp();
    server.use(
      getGetApiUsersUserIdAwayLocationsMockHandler(() => {
        throw new Error('Network error');
      }),
    );

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.text()).toContain('Failed to load away locations');
  });

  it('shows Add Away Location button when user has AwayLocationsCreate permission', async () => {
    const app = await createTestApp({ permissions: [Permissions.AwayLocationsCreate] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => []));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Away Location'),
    );
    expect(addButton).toBeDefined();

    wrapper.unmount();
  });

  it('hides Add Away Location button when user lacks AwayLocationsCreate permission', async () => {
    const app = await createTestApp({ permissions: [] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => []));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Away Location'),
    );
    expect(addButton).toBeUndefined();

    wrapper.unmount();
  });

  it('opens create modal when Add Away Location button is clicked', async () => {
    const app = await createTestApp({ permissions: [Permissions.AwayLocationsCreate] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => []));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Away Location'),
    );
    addButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain('Add Away Location');

    wrapper.unmount();
  });

  it('shows edit and expire action buttons when user has both permissions', async () => {
    const app = await createTestApp({
      permissions: [Permissions.AwayLocationsEdit, Permissions.AwayLocationsExpire],
    });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const editButtons = Array.from(document.querySelectorAll('[aria-label="Edit away location"]'));
    const expireButtons = Array.from(document.querySelectorAll('[aria-label="Expire away location"]'));

    expect(editButtons.length).toBeGreaterThan(0);
    expect(expireButtons.length).toBeGreaterThan(0);

    wrapper.unmount();
  });

  it('hides action buttons when user lacks edit and expire permissions', async () => {
    const app = await createTestApp({ permissions: [] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const editButtons = Array.from(document.querySelectorAll('[aria-label="Edit away location"]'));
    const expireButtons = Array.from(document.querySelectorAll('[aria-label="Expire away location"]'));

    expect(editButtons.length).toBe(0);
    expect(expireButtons.length).toBe(0);

    wrapper.unmount();
  });

  it('opens edit modal when edit button is clicked', async () => {
    const app = await createTestApp({ permissions: [Permissions.AwayLocationsEdit] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const editButton = document.querySelector('[aria-label="Edit away location"]');
    editButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain('Edit Away Location');

    wrapper.unmount();
  });

  it('opens expire modal when expire button is clicked', async () => {
    const app = await createTestApp({ permissions: [Permissions.AwayLocationsExpire] });
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const expireButton = document.querySelector('[aria-label="Expire away location"]');
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain('Expire Away Location');

    wrapper.unmount();
  });

  it('shows comment in the table when present', async () => {
    const app = await createTestApp();
    server.use(getGetApiUsersUserIdAwayLocationsMockHandler(() => awayLocationFixtures));

    const wrapper = mount(AwayLocations, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();

    expect(wrapper.text()).toContain('Training visit');
  });
});
