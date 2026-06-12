import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import UaSelect from '@/shared/components/UaSelect.vue';
import ExpireRoleModal from '@/modules/myteam/components/ExpireRoleModal.vue';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { UserRoleResponseDto } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('ExpireRoleModal', () => {
  const assignment: UserRoleResponseDto = {
    id: 7002,
    userId: 'test-user-id',
    roleId: 7,
    effectiveDate: '2026-01-10T00:00:00.000Z',
    expiryDate: null,
    expiryReason: null,
  };

  it('requires expiry reason before submitting', async () => {
    const app = await createTestApp();

    const wrapper = mount(ExpireRoleModal, {
      props: {
        userId: 'test-user-id',
        assignment,
        roleName: 'Scheduler',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const expireButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Expire'),
    );
    expect(expireButton).toBeDefined();
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(document.body.textContent ?? '').toContain('Reason for expiry is required.');

    wrapper.unmount();
  });

  it('calls dedicated expire endpoint and emits saved + close on success', async () => {
    const app = await createTestApp();

    let requestBody: Record<string, unknown> | null = null;

    server.use(
      http.post('*/api/users/:id/roles/expire', async ({ request, params }) => {
        requestBody = (await request.json()) as Record<string, unknown>;
        expect(params.id).toBe('test-user-id');

        return HttpResponse.json({
          id: 7002,
          userId: 'test-user-id',
          roleId: 7,
          effectiveDate: '2026-01-10T00:00:00.000Z',
          expiryDate: '2026-06-09T00:00:00.000Z',
          expiryReason: 'PERSONAL',
        });
      }),
    );

    const wrapper = mount(ExpireRoleModal, {
      props: {
        userId: 'test-user-id',
        assignment,
        roleName: 'Scheduler',
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const reasonSelect = wrapper.findComponent(UaSelect);
    reasonSelect.vm.$emit('update:modelValue', 'PERSONAL');

    await flushPromises();

    const expireButton = Array.from(document.querySelectorAll('button')).find((button) =>
      button.textContent?.includes('Expire'),
    );
    expect(expireButton).toBeDefined();
    expireButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(requestBody).not.toBeNull();

    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();

    wrapper.unmount();
  });
});
