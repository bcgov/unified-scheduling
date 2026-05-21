import { afterEach, describe, it, expect } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { createTestApp } from '../../../helpers/createTestApp';
import RoleFormModal from '@/modules/myteam/components/RoleFormModal.vue';
import {
  getGetApiPermissionsMockHandler,
  getGetApiPermissionsResponseMock,
} from '@/api-access/generated/permissions/permissions.msw';
import { getPostApiRolesMockHandler } from '@/api-access/generated/roles/roles.msw';
import { server } from '../../../mocks/server';
import type { PermissionDto, RoleDto } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('RoleFormModal', () => {
  it('renders create role form when role prop is null', async () => {
    const app = await createTestApp();
    const permissions = getGetApiPermissionsResponseMock();
    server.use(getGetApiPermissionsMockHandler(() => permissions));
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('Create Role');
    expect(content).toContain('Role Name');
    expect(content).toContain('Description');
    wrapper.unmount();
  });

  it('renders edit role form when role prop is provided', async () => {
    const app = await createTestApp();
    const permissions = getGetApiPermissionsResponseMock();
    const role: RoleDto = {
      id: 1,
      name: 'Existing Role',
      description: 'Existing description',
      concurrencyToken: 0,
      permissions: [],
    };
    server.use(getGetApiPermissionsMockHandler(() => permissions));
    const wrapper = mount(RoleFormModal, {
      props: { role },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('Edit Role');
    // Check that the form title indicates edit mode (which is shown when role is provided)
    const titleElement = document.querySelector('[class*="modal"] [class*="title"]');
    expect(titleElement?.textContent).toContain('Edit Role');
    wrapper.unmount();
  });

  it('loads and displays all available permissions', async () => {
    const app = await createTestApp();
    const permissions: PermissionDto[] = [
      { id: '1', description: 'View Dashboard' },
      { id: '2', description: 'Edit Users' },
      { id: '3', description: 'Delete Roles' },
    ];
    server.use(getGetApiPermissionsMockHandler(() => permissions));
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('View Dashboard');
    expect(content).toContain('Edit Users');
    expect(content).toContain('Delete Roles');
    wrapper.unmount();
  });

  it('shows error message when permissions fail to load', async () => {
    const app = await createTestApp();
    server.use(
      getGetApiPermissionsMockHandler(() => {
        throw new Error('Failed to load permissions');
      }),
    );
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('Failed to load permissions');
    wrapper.unmount();
  });

  it('shows validation errors if required fields are empty', async () => {
    const app = await createTestApp();
    const permissions = getGetApiPermissionsResponseMock();
    server.use(getGetApiPermissionsMockHandler(() => permissions));
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const saveButton = Array.from(document.querySelectorAll('button')).find(
      (btn) => btn.textContent?.includes('Create Role') || btn.textContent?.includes('Save Changes'),
    );
    expect(saveButton).toBeDefined();
    (saveButton as HTMLButtonElement).click();
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('Role name is required');
    expect(content).toContain('Description is required');
    expect(content).toContain('At least one permission must be selected');
    wrapper.unmount();
  });

  it('submits form with valid data to create a new role', async () => {
    const app = await createTestApp();
    const permissions = [{ id: '1', description: 'Permission 1' }];
    server.use(
      getGetApiPermissionsMockHandler(() => permissions),
      getPostApiRolesMockHandler(() => ({
        id: 1,
        name: 'New Role',
        description: 'New description',
        concurrencyToken: 0,
        permissions: [],
      })),
    );
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const nameField = document.querySelector('#role-name') as HTMLInputElement;
    const descField = document.querySelector('#role-description') as HTMLInputElement;
    const checkbox = document.querySelector('input[type="checkbox"]') as HTMLInputElement;

    nameField.value = 'New Role';
    nameField.dispatchEvent(new Event('input', { bubbles: true }));
    descField.value = 'New description';
    descField.dispatchEvent(new Event('input', { bubbles: true }));
    checkbox.click();

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find(
      (btn) => btn.textContent?.includes('Create Role') || btn.textContent?.includes('Save'),
    );
    (saveButton as HTMLButtonElement)?.click();
    await flushPromises();

    const emitted = wrapper.emitted('created');
    expect(emitted).toBeTruthy();
    wrapper.unmount();
  });

  it('displays no permissions message when permissions list is empty', async () => {
    const app = await createTestApp();
    server.use(getGetApiPermissionsMockHandler(() => []));
    const wrapper = mount(RoleFormModal, {
      props: { role: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    const content = document.body.textContent ?? '';
    expect(content).toContain('No permissions available');
    wrapper.unmount();
  });
});
