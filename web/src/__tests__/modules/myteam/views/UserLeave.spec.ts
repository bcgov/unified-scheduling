import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import { getGetApiUsersIdResponseMock, getGetApiUsersMockHandler } from '@/api-access/generated/users/users.msw';
import UserLeave from '@/modules/myteam/views/UserLeave.vue';
import { mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';

describe('UserLeave', () => {
  it('sorts standalone leave and recurring series by start date and allows changing the sort', async () => {
    const user = getGetApiUsersIdResponseMock({ id: 'current-user' });
    server.use(
      getGetApiUsersMockHandler([user]),
      http.get('*/api/leave/leave-types', () => HttpResponse.json([])),
      http.get('*/api/scheduling/leave/users/current-user/entries', () =>
        HttpResponse.json([
          { id: 3, leaveSeriesId: 77, startAtUtc: '2026-06-25T02:00:00Z', timeZoneId: 'America/Vancouver' },
          { id: 2, allDay: true, leaveDate: '2026-06-11' },
          { id: 1, allDay: true, leaveDate: '2026-06-05' },
        ]),
      ),
      http.get('*/api/scheduling/leave/series/77', () =>
        HttpResponse.json({
          id: 77,
          startAtUtc: '2026-06-11T02:00:00Z',
          endAtUtc: '2026-06-11T04:00:00Z',
          timeZoneId: 'America/Vancouver',
          recurrenceRule: 'FREQ=WEEKLY;INTERVAL=2;COUNT=4',
        }),
      ),
    );
    const app = await createTestApp({ loadConfig: false, permissions: [Permissions.UsersLeaveView] });
    const wrapper = mount(UserLeave, {
      props: { user },
      global: { plugins: app.mountPlugins },
    });

    const dates = () => wrapper.findAll('tbody tr > td:nth-child(3)').map((cell) => cell.text());
    await vi.waitFor(() => expect(dates()).toEqual(['2026-06-11', '2026-06-10', '2026-06-05']));

    const dateHeader = wrapper.get('thead th[aria-sort="descending"]');
    expect(dateHeader.text()).toBe('Date');
    await dateHeader.trigger('click');
    await dateHeader.trigger('click');

    expect(dateHeader.attributes('aria-sort')).toBe('ascending');
    expect(dates()).toEqual(['2026-06-05', '2026-06-10', '2026-06-11']);
    wrapper.unmount();
  });
});
