import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import AssignRoleModal from '@/modules/myteam/components/AssignRoleModal.vue';
import {
  getPostApiUsersIdRolesMockHandler,
  getPostApiUsersIdRolesResponseMock,
} from '@/api-access/generated/users/users.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { RoleDto, UserRoleResponseDto } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('AssignRoleModal', () => {
  const roles: RoleDto[] = [
    {
      id: 7,
      name: 'Scheduler',
      description: 'Scheduler role',
      concurrencyToken: 0,
      permissions: [],
    },
  ];

  it('shows validation error when role is not selected', async () => {
    const app = await createTestApp();

    const wrapper = mount(AssignRoleModal, {
      props: {
        userId: 'test-user-id',
        roles,
        assignment: null,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    expect(saveButton).toBeDefined();
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Role is required.');

    wrapper.unmount();
  });

  it('submits assignment payload with expiryDate and emits saved + close', async () => {
    const app = await createTestApp();
    const assignment: UserRoleResponseDto = {
      id: 7001,
      userId: 'test-user-id',
      roleId: 7,
      effectiveDate: '2026-01-10T00:00:00.000-08:00',
      expiryDate: '2026-01-21T23:59:59.999-08:00',
      expiryReason: null,
    };

    let requestBody: Record<string, unknown> | null = null;

    server.use(
      getPostApiUsersIdRolesMockHandler(async (info) => {
        requestBody = (await info.request.json()) as Record<string, unknown>;
        return getPostApiUsersIdRolesResponseMock({
          roleId: 7,
          userId: 'test-user-id',
          effectiveDate: '2026-01-10T00:00:00.000-08:00',
          expiryDate: '2026-01-21T23:59:59.999-08:00',
          expiryReason: null,
        });
      }),
    );

    const wrapper = mount(AssignRoleModal, {
      props: {
        userId: 'test-user-id',
        roles,
        assignment,
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Save'),
    );
    expect(saveButton).toBeDefined();
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(requestBody).toEqual({
      roleId: 7,
      effectiveDate: '2026-01-10',
      expiryDate: '2026-01-21',
    });

    expect(requestBody).not.toHaveProperty('expiryReason');

    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });
});
