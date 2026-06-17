import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import type { RoleAssignedUserDto, RoleDto } from '@/api-access/generated/models';
import {
  getDeleteApiRolesIdMockHandler,
  getGetApiRolesIdUsersMockHandler,
  getPostApiRolesIdReassingAndDeleteMockHandler,
} from '@/api-access/generated/roles/roles.msw';
import RoleDeleteModal from '@/modules/myteam/components/RoleDeleteModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { createTestApp } from '../../../helpers/createTestApp';
import { server } from '../../../mocks/server';

afterEach(() => {
  document.body.innerHTML = '';
  vi.restoreAllMocks();
});

describe('RoleDeleteModal', () => {
  const getDocumentText = () => document.body.textContent ?? '';

  const setDateInputValue = (selector: string, value: string) => {
    const input = document.querySelector(selector) as HTMLInputElement | null;
    expect(input).not.toBeNull();
    input!.value = value;
    input!.dispatchEvent(new Event('input', { bubbles: true }));
    input!.dispatchEvent(new Event('change', { bubbles: true }));
  };

  const role: RoleDto = {
    id: 1,
    name: 'Manager',
    description: 'Manager role',
    concurrencyToken: 0,
    permissions: [],
  };

  const allRoles: RoleDto[] = [
    role,
    {
      id: 2,
      name: 'Supervisor',
      description: 'Supervisor role',
      concurrencyToken: 0,
      permissions: [],
    },
  ];

  const assignedUsers: RoleAssignedUserDto[] = [
    {
      userId: '0c96fdb9-1d1e-4a70-9c1f-f89d739ea001',
      firstName: 'Alex',
      lastName: 'Morgan',
      email: 'alex.morgan@example.com',
      isEnabled: true,
    },
  ];

  it('calls the delete endpoint and emits deleted + close when no users are assigned', async () => {
    const app = await createTestApp();
    let deleteCalled = false;

    server.use(
      getGetApiRolesIdUsersMockHandler(() => []),
      getDeleteApiRolesIdMockHandler(({ params }) => {
        deleteCalled = true;
        expect(params.id).toBe(String(role.id));

        return {
          id: role.id,
          deletedBy: '5ac9ef5d-b4f2-4726-b6bf-08f3f7f95d70',
          deletedOn: '2026-06-17T10:00:00Z',
        };
      }),
    );

    const wrapper = mount(RoleDeleteModal, {
      props: { role, allRoles },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const deleteButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Delete'),
    );

    expect(deleteButton).toBeDefined();
    deleteButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(deleteCalled).toBe(true);
    expect(wrapper.emitted('deleted')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('calls the reassign-and-delete endpoint with date-only payload when users are assigned', async () => {
    const app = await createTestApp();
    let requestBody: Record<string, unknown> | null = null;

    server.use(
      getGetApiRolesIdUsersMockHandler(() => assignedUsers),
      getPostApiRolesIdReassingAndDeleteMockHandler(async ({ request, params }) => {
        expect(params.id).toBe(String(role.id));
        requestBody = (await request.json()) as Record<string, unknown>;

        return {
          id: role.id,
          deletedBy: '5ac9ef5d-b4f2-4726-b6bf-08f3f7f95d70',
          deletedOn: '2026-06-17T10:00:00Z',
        };
      }),
    );

    const wrapper = mount(RoleDeleteModal, {
      props: { role, allRoles },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const roleSelect = wrapper.findComponent(UaSelect);
    roleSelect.vm.$emit('update:modelValue', 2);

    setDateInputValue('#reassignment-effective-date', '2026-02-01');
    setDateInputValue('#reassignment-expiry-date', '2026-02-15');

    await flushPromises();

    const reassignButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Reassign and Delete'),
    );

    expect(reassignButton).toBeDefined();
    reassignButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(requestBody).toEqual({
      newRoleId: 2,
      newRoleEffectiveDate: '2026-02-01',
      newRoleExpiryDate: '2026-02-15',
    });
    expect(wrapper.emitted('deleted')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });

  it('disables reassignment when expiry date is not after effective date', async () => {
    const app = await createTestApp();

    server.use(getGetApiRolesIdUsersMockHandler(() => assignedUsers));

    const wrapper = mount(RoleDeleteModal, {
      props: { role, allRoles },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const roleSelect = wrapper.findComponent(UaSelect);
    roleSelect.vm.$emit('update:modelValue', 2);

    setDateInputValue('#reassignment-effective-date', '2026-02-15');
    setDateInputValue('#reassignment-expiry-date', '2026-02-15');

    await flushPromises();

    const reassignButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Reassign and Delete'),
    ) as HTMLButtonElement | undefined;

    expect(getDocumentText()).toContain('Expiry date must be after effective date.');
    expect(reassignButton).toBeDefined();
    expect(reassignButton?.disabled).toBe(true);

    wrapper.unmount();
  });

  it('shows API problem details when deletion fails', async () => {
    const app = await createTestApp();

    server.use(
      getGetApiRolesIdUsersMockHandler(() => []),
      http.delete('*/api/roles/:id', () => {
        return HttpResponse.json({ detail: 'Role cannot be deleted while dependencies exist.' }, { status: 400 });
      }),
    );

    const wrapper = mount(RoleDeleteModal, {
      props: { role, allRoles },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const deleteButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Delete'),
    );

    expect(deleteButton).toBeDefined();
    deleteButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(getDocumentText()).toContain('Role cannot be deleted while dependencies exist.');
    expect(wrapper.emitted('deleted')).toBeFalsy();

    wrapper.unmount();
  });
});
