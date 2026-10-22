import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import ActingPositions from '@/modules/myteam/views/ActingPositions.vue';
import { getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import { createTestApp } from '../../../helpers/createTestApp';
import { server } from '../../../mocks/server';

describe('acting position lookup failure', () => {
  it('keeps the list usable, disables rank-dependent actions and recovers on retry', async () => {
    let requests = 0;
    server.use(
      http.get('*/api/lookup/PositionTypes', () => {
        requests++;
        return requests === 1
          ? new HttpResponse(null, { status: 500 })
          : HttpResponse.json([{ code: 'Sergeant', description: 'Sergeant', displayOrder: 1 }]);
      }),
    );
    const app = await createTestApp({ permissions: ['ActingPositionsCreate' as never] });
    const wrapper = mount(ActingPositions, {
      props: { user: getGetApiUsersIdResponseMock() },
      global: { plugins: app.mountPlugins },
    });
    await flushPromises();
    expect(wrapper.text()).toContain('Position options are unavailable');
    const add = wrapper.findAll('button').find((button) => button.text().includes('Add Acting Position'));
    expect(add?.attributes('disabled')).toBeDefined();
    await wrapper
      .findAll('button')
      .find((button) => button.text().includes('Retry'))
      ?.trigger('click');
    await flushPromises();
    expect(wrapper.text()).not.toContain('Position options are unavailable');
    expect(add?.attributes('disabled')).toBeUndefined();
    expect(requests).toBe(2);
    wrapper.unmount();
  });
});
