import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '../../../helpers/createTestApp';
import RolesPermissions from '@/modules/myteam/views/RolesPermissions.vue';
import { server } from '../../../mocks/server';
import { getGetApiRolesMockHandler } from '@/api-access/generated/roles/roles.msw';
import { Permissions, type RoleDto } from '@/api-access/generated/models';

describe('RolesPermissions', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
  });

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('renders roles table when roles are loaded', async () => {
    const app = await createTestApp();
    const roles = [
      { id: 1, name: 'Admin', description: 'Administrator role', concurrencyToken: 0, permissions: [] },
      { id: 2, name: 'Editor', description: 'Editor role', concurrencyToken: 0, permissions: [] },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('Roles & Permissions');
    for (const role of roles) {
      expect(wrapper.text()).toContain(role.name);
      expect(wrapper.text()).toContain(role.description);
    }
  });

  it('shows empty state when no roles exist', async () => {
    const app = await createTestApp();
    server.use(getGetApiRolesMockHandler(() => []));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('No roles available');
    expect(wrapper.text()).toContain('Create your first role to get started');
  });

  it('shows error alert when roles API fails', async () => {
    const app = await createTestApp();
    server.use(
      getGetApiRolesMockHandler(() => {
        throw new Error('Network error');
      }),
    );
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('Failed to load roles');
  });

  it('displays multiple roles in the table', async () => {
    const app = await createTestApp();
    const roles = [
      { id: 1, name: 'Admin', description: 'Administrator role', concurrencyToken: 0, permissions: [] },
      { id: 2, name: 'Editor', description: 'Editor role', concurrencyToken: 0, permissions: [] },
      { id: 3, name: 'Viewer', description: 'View-only role', concurrencyToken: 0, permissions: [] },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    for (const role of roles) {
      expect(wrapper.text()).toContain(role.name);
    }
  });

  it('renders Add Role button when user has permission', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesCreate],
    });
    server.use(getGetApiRolesMockHandler(() => []));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Role'),
    );
    expect(addButton).toBeDefined();
    wrapper.unmount();
  });

  it('hides Add Role button when user lacks permission', async () => {
    const app = await createTestApp({
      permissions: [],
    });
    server.use(getGetApiRolesMockHandler(() => []));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Role'),
    );
    expect(addButton).toBeUndefined();
    wrapper.unmount();
  });

  it('opens create modal when Add Role button is clicked', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesCreate],
    });
    server.use(getGetApiRolesMockHandler(() => []));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const addButton = Array.from(document.querySelectorAll('button')).find((btn) =>
      btn.textContent?.includes('Add Role'),
    );
    (addButton as HTMLButtonElement)?.click();
    await flushPromises();
    const createRoleTitle = Array.from(document.querySelectorAll('div')).find((el) =>
      el.textContent?.includes('Create Role'),
    );
    expect(createRoleTitle).toBeDefined();
    wrapper.unmount();
  });

  it('shows edit button for roles when user has permission', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesEdit],
    });
    const roles = [{ id: 1, name: 'Admin', description: 'Admin', concurrencyToken: 0, permissions: [] }];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const editButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Edit role'),
    );
    expect(editButtons.length).toBeGreaterThan(0);
    wrapper.unmount();
  });

  it('hides edit button when user lacks edit permission', async () => {
    const app = await createTestApp({
      permissions: [],
    });
    const roles = [{ id: 1, name: 'Admin', description: 'Admin', concurrencyToken: 0, permissions: [] }];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const editButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Edit role'),
    );
    expect(editButtons.length).toBe(0);
    wrapper.unmount();
  });

  it('shows delete button for roles when user has permission', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesExpire],
    });
    const roles = [{ id: 1, name: 'Admin', description: 'Admin', concurrencyToken: 0, permissions: [] }];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const deleteButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Delete role'),
    );
    expect(deleteButtons.length).toBeGreaterThan(0);
    wrapper.unmount();
  });

  it('hides delete button when user lacks expire permission', async () => {
    const app = await createTestApp({
      permissions: [],
    });
    const roles = [{ id: 1, name: 'Admin', description: 'Admin', concurrencyToken: 0, permissions: [] }];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const deleteButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Delete role'),
    );
    expect(deleteButtons.length).toBe(0);
    wrapper.unmount();
  });

  it('renders card title when roles are present', async () => {
    const app = await createTestApp();
    const roles = [{ id: 1, name: 'Admin', description: 'Admin', concurrencyToken: 0, permissions: [] }];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('Available Roles');
  });

  it('hides inactive roles by default (active filter)', async () => {
    const app = await createTestApp();
    const roles: RoleDto[] = [
      { id: 1, name: 'Active Role', description: 'Active', concurrencyToken: 0, permissions: [], deletedOn: null },
      {
        id: 2,
        name: 'Deleted Role',
        description: 'Inactive',
        concurrencyToken: 0,
        permissions: [],
        deletedOn: '2026-01-01T00:00:00Z',
      },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(wrapper.text()).toContain('Active Role');
    expect(wrapper.text()).not.toContain('Deleted Role');
  });

  it('shows inactive roles when Inactive filter is selected', async () => {
    const app = await createTestApp();
    const roles: RoleDto[] = [
      { id: 1, name: 'Active Role', description: 'Active', concurrencyToken: 0, permissions: [], deletedOn: null },
      {
        id: 2,
        name: 'Deleted Role',
        description: 'Inactive',
        concurrencyToken: 0,
        permissions: [],
        deletedOn: '2026-01-01T00:00:00Z',
      },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins } });
    await flushPromises();

    // Switch filter to Inactive
    const select = wrapper.findComponent({ name: 'UaSelect' });
    await select.setValue('inactive');
    await flushPromises();

    expect(wrapper.text()).toContain('Deleted Role');
    expect(wrapper.text()).not.toContain('Active Role');
  });

  it('hides action buttons for inactive roles', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesEdit, Permissions.RolesExpire],
    });
    const roles: RoleDto[] = [
      {
        id: 1,
        name: 'Inactive Role',
        description: 'Inactive',
        concurrencyToken: 0,
        permissions: [],
        deletedOn: '2026-01-01T00:00:00Z',
      },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));

    // Switch to inactive filter so the role is visible
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();
    const select = wrapper.findComponent({ name: 'UaSelect' });
    await select.setValue('inactive');
    await flushPromises();

    const editButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Edit role'),
    );
    const deleteButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Delete role'),
    );
    expect(editButtons.length).toBe(0);
    expect(deleteButtons.length).toBe(0);
    wrapper.unmount();
  });

  it('shows action buttons for active roles', async () => {
    const app = await createTestApp({
      permissions: [Permissions.RolesEdit, Permissions.RolesExpire],
    });
    const roles: RoleDto[] = [
      { id: 1, name: 'Active Role', description: 'Active', concurrencyToken: 0, permissions: [], deletedOn: null },
    ];
    server.use(getGetApiRolesMockHandler(() => roles));
    const wrapper = mount(RolesPermissions, { global: { plugins: app.mountPlugins }, attachTo: document.body });
    await flushPromises();

    const editButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Edit role'),
    );
    const deleteButtons = Array.from(document.querySelectorAll('button')).filter((btn) =>
      btn.title?.includes('Delete role'),
    );
    expect(editButtons.length).toBeGreaterThan(0);
    expect(deleteButtons.length).toBeGreaterThan(0);
    wrapper.unmount();
  });
});
