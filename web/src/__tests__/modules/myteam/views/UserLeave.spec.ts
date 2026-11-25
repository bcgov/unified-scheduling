import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions, type UserResponse } from '@/api-access/generated/models';
import { getGetApiUsersIdResponseMock, getGetApiUsersMockHandler } from '@/api-access/generated/users/users.msw';
import UserLeave from '@/modules/myteam/views/UserLeave.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { defineComponent } from 'vue';
import { describe, expect, it, vi } from 'vitest';

const user = { id: '00000000-0000-0000-0000-000000000001' } as UserResponse;
const series = { id: 77, statusTypeCode: 'Active', recurrenceRule: 'FREQ=DAILY;COUNT=1', userIds: [user.id] };
const entries = [
  { id: 1, statusTypeCode: 'Draft' },
  { id: 2, statusTypeCode: 'Active' },
  { id: 3, statusTypeCode: 'Cancelled' },
  { id: 4, statusTypeCode: 'Active', leaveSeriesId: series.id },
];

const UaDataTableStub = defineComponent({
  props: ['items'],
  template: `
    <div v-for="item in items" :key="item.rowId" :data-row="item.rowId">
      <slot name="item.actions" :item="item" />
    </div>
  `,
});

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

  it.each([true, false])('respects status and My Team edit permission (allowed: %s)', async (allowed) => {
    server.use(
      http.get('*/api/scheduling/leave/users/:userId/entries', () => HttpResponse.json(entries)),
      http.get('*/api/scheduling/leave/series/77', () => HttpResponse.json(series)),
      http.get('*/api/leave/leave-types', () => HttpResponse.json([])),
      http.get('*/api/users*', () => HttpResponse.json([])),
    );
    const app = await createTestApp({
      loadConfig: false,
      permissions: [
        Permissions.UsersLeaveView,
        Permissions.UsersLeaveDelete,
        ...(allowed ? [Permissions.UsersLeaveEdit] : []),
      ],
    });
    const wrapper = mount(UserLeave, {
      props: { user },
      global: {
        plugins: app.mountPlugins,
        stubs: { UaDataTable: UaDataTableStub, UaBtn: true, VIcon: true, UserLeaveModal: true },
      },
    });
    await vi.waitFor(() => expect(wrapper.findAll('[data-row]')).toHaveLength(4));

    const edit = '[aria-label="Edit leave record"]';
    const remove = '[aria-label="Delete leave record"]';
    expect(wrapper.get('[data-row="entry-1"]').find(edit).exists()).toBe(allowed);
    expect(wrapper.get('[data-row="entry-1"]').find(remove).exists()).toBe(true);
    expect(wrapper.get('[data-row="entry-2"]').find(edit).exists()).toBe(allowed);
    expect(wrapper.get('[data-row="entry-2"]').find(remove).exists()).toBe(false);
    expect(wrapper.get('[data-row="entry-3"]').find(edit).exists()).toBe(false);
    expect(wrapper.get('[data-row="entry-3"]').find(remove).exists()).toBe(false);

    const seriesRow = wrapper.get('[data-row="series-77"]');
    const editSeries = '[aria-label="Edit leave series"]';
    expect(seriesRow.find(editSeries).exists()).toBe(allowed);
    if (allowed) {
      await seriesRow.get(editSeries).trigger('click');
      await flushPromises();
      expect(wrapper.getComponent({ name: 'UserLeaveModal' }).props('scope')).toBe('series');
    }
    wrapper.unmount();
  });
});
