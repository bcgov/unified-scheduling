import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import {
  getGetApiSchedulingAssignmentsEntriesMockHandler,
  getGetApiSchedulingAssignmentsSeriesIdMockHandler,
} from '@/api-access/generated/assignment/assignment.msw';
import {
  getGetApiSchedulingShiftsEntriesIdMockHandler,
  getGetApiSchedulingShiftsSeriesIdMockHandler,
} from '@/api-access/generated/shift/shift.msw';
import { getGetApiUsersIdMockHandler, getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import { Permissions } from '@/api-access/generated/models';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingShiftDetailModal from '@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue';

enableAutoUnmount(afterEach);

// Before B.C.'s 2026 time change, winter offsets differ in Vancouver and Edmonton.
const entry = {
  id: 42,
  title: 'Occurrence',
  statusTypeCode: 'Draft',
  startAtUtc: '2026-01-26T17:00:00Z',
  endAtUtc: '2026-01-26T19:00:00Z',
  timeZoneId: 'America/Vancouver',
  createdById: 'entry-creator',
  createdOn: '2026-01-20T17:15:00Z',
  updatedById: 'entry-updater',
  updatedOn: '2026-01-24T22:30:00Z',
};

const series = {
  ...entry,
  id: 202,
  title: 'Recurring series',
  recurrenceRule: 'FREQ=DAILY;COUNT=2',
  createdById: 'series-creator',
  createdOn: '2026-01-19T17:15:00Z',
  updatedById: 'series-updater',
};

const stubs = {
  UaModal: {
    template: '<div><slot name="alerts" /><slot name="secondary-header" /><slot /><slot name="actions" /></div>',
  },
};

function mockAuditUsers() {
  const names: Record<string, string> = {
    'entry-creator': 'Jane Smith',
    'entry-updater': 'John Doe',
    'series-creator': 'Taylor Ng',
    'series-updater': 'Sam Torres',
  };
  return getGetApiUsersIdMockHandler(({ params }) =>
    getGetApiUsersIdResponseMock({ id: String(params.id), firstName: names[String(params.id)], lastName: '' }),
  );
}

describe('Scheduling details audit', () => {
  it('shows shift occurrence and series audits in their active time zones and hides them on Delete', async () => {
    const shiftSeries = { ...series, timeZoneId: 'America/Edmonton', updatedOn: null };
    server.use(
      mockAuditUsers(),
      getGetApiSchedulingShiftsEntriesIdMockHandler(entry),
      getGetApiSchedulingShiftsSeriesIdMockHandler(shiftSeries),
    );
    const app = await createTestApp({ loadConfig: false, permissions: [Permissions.ShiftsDelete] });
    const event = {
      id: 'shift-42',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: entry.title,
      statusTypeCode: entry.statusTypeCode,
      start: entry.startAtUtc,
      end: entry.endAtUtc,
      timeZoneId: entry.timeZoneId,
      metadata: { shiftEntryId: '42', shiftSeriesId: 202 },
    };
    const wrapper = mount(CalendarSchedulingShiftDetailModal, {
      props: { event, initialOpenScope: 'event' },
      global: { plugins: app.mountPlugins, stubs },
    });

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Created by:');
      expect(wrapper.text()).toContain('Jane Smith');
      expect(wrapper.text()).toContain('January 20, 2026 at 9:15 AM');
      expect(wrapper.text()).toContain('Last updated by:');
      expect(wrapper.text()).toContain('John Doe');
      expect(wrapper.text()).toContain('January 24, 2026 at 2:30 PM');
    });

    const deleteTab = wrapper.findAll('[role="tab"]').find((tab) => tab.text() === 'Delete');
    expect(deleteTab).toBeDefined();
    await deleteTab!.trigger('click');
    expect(wrapper.text()).not.toContain('Created by:');
    expect(wrapper.text()).not.toContain('Last updated by:');

    await wrapper.setProps({ event: { ...event }, initialOpenScope: undefined });
    expect(wrapper.text()).not.toContain('Created by:');
    const seriesButton = wrapper.findAll('button').find((button) => button.text() === 'The entire series');
    expect(seriesButton).toBeDefined();
    await seriesButton!.trigger('click');
    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Taylor Ng');
      expect(wrapper.text()).toContain('January 19, 2026 at 10:15 AM');
      expect(wrapper.text()).not.toContain('Jane Smith');
      expect(wrapper.text()).not.toContain('Last updated by:');
    });
  });

  it('shows assignment audits for the current scope and clears them during loading and after failed or legacy loads', async () => {
    let userLookupDenied = false;
    let completeSeriesLoad!: (response: typeof series) => void;
    const pendingSeries = new Promise<typeof series>((resolve) => {
      completeSeriesLoad = resolve;
    });
    server.use(
      http.get('*/api/users/entry-creator', () => {
        userLookupDenied = true;
        return new HttpResponse(null, { status: 403 });
      }),
      mockAuditUsers(),
      http.get('*/api/scheduling/assignments/entries/42', () => HttpResponse.json({ ...entry, updatedOn: null })),
      http.get('*/api/scheduling/assignments/entries/43', () =>
        HttpResponse.json({ id: 43, title: 'Legacy assignment', statusTypeCode: 'Draft' }),
      ),
      http.get('*/api/scheduling/assignments/entries/44', () =>
        HttpResponse.json({ message: 'Assignment was not found.' }, { status: 404 }),
      ),
      getGetApiSchedulingAssignmentsSeriesIdMockHandler(() => pendingSeries),
      getGetApiSchedulingAssignmentsEntriesMockHandler([]),
    );
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode: 'view',
        assignmentEntryId: 42,
        assignmentSeriesId: 202,
        editScope: 'event',
        timeZone: 'America/Vancouver',
      },
      global: { plugins: app.mountPlugins, stubs },
    });

    await vi.waitFor(() => {
      expect(userLookupDenied).toBe(true);
      expect(wrapper.text()).toContain('Occurrence');
      expect(wrapper.text()).toContain('Created by:');
      expect(wrapper.text()).toContain('Unknown user');
      expect(wrapper.text()).toContain('January 20, 2026 at 9:15 AM');
      expect(wrapper.text()).not.toContain('Last updated by:');
    });

    await wrapper.setProps({ editScope: 'series' });
    expect(wrapper.find('[aria-label="Loading Details"]').exists()).toBe(true);
    expect(wrapper.text()).not.toContain('Created by:');
    completeSeriesLoad(series);
    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Taylor Ng');
      expect(wrapper.text()).toContain('January 19, 2026 at 9:15 AM');
      expect(wrapper.text()).toContain('Last updated by:');
      expect(wrapper.text()).toContain('Sam Torres');
      expect(wrapper.text()).toContain('January 24, 2026 at 2:30 PM');
    });

    await wrapper.setProps({ assignmentEntryId: 44, editScope: 'event' });
    await vi.waitFor(() => expect(wrapper.text()).toContain('Assignment was not found.'));
    expect(wrapper.text()).not.toContain('Created by:');
    expect(wrapper.text()).not.toContain('Last updated by:');

    await wrapper.setProps({ assignmentEntryId: 43 });
    await vi.waitFor(() => expect(wrapper.text()).toContain('Legacy assignment'));
    expect(wrapper.text()).not.toContain('Created by:');
    expect(wrapper.text()).not.toContain('Last updated by:');
  });
});
