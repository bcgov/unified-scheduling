import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { IANAZone } from 'luxon';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { Permissions, type CalendarConflictResponse } from '@/api-access/generated/models';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import AddResourceModal from '@/modules/scheduling/CalendarSchedulingAddResourceModal.vue';
import AssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import LeaveModal from '@/modules/scheduling/CalendarSchedulingLeaveDetailModal.vue';

const userId = '11111111-1111-4111-8111-111111111111';
const mounted: VueWrapper[] = [];
afterEach(() => {
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  vi.restoreAllMocks();
  document.body.innerHTML = '';
});

async function prepareModal(kind: 'shift' | 'assignment' | 'leave', series: boolean, edit = false) {
  const writes: Array<{ path: string; body: Record<string, unknown> }> = [];
  const resource = {
    id: series ? 42 : 41,
    title: 'Coverage',
    notes: 'Original notes',
    concurrencyToken: 7,
    startAtUtc: '2026-07-13T16:00:00Z',
    endAtUtc: '2026-07-14T00:00:00Z',
    timeZoneId: 'America/Vancouver',
    locationId: 1,
    userIds: [userId],
    leaveTypeId: 9,
    assignmentDefinitionId: 100,
    categoryId: 10,
    subCategoryId: 11,
    capacity: 1,
    color: 'white',
    statusTypeCode: 'Draft',
    recurrenceRule: series ? 'FREQ=DAILY;COUNT=2' : null,
    assignedUserIds: [],
    linkedShiftEntryIds: [],
    assignmentLinks: [],
    shiftSeriesLinks: [],
  };
  server.use(
    http.all('*/api/*', async ({ request }) => {
      const path = new URL(request.url).pathname;
      if (request.method !== 'GET') {
        writes.push({ path, body: (await request.json().catch(() => ({}))) as Record<string, unknown> });
        return HttpResponse.json({ ...resource, concurrencyToken: 8 });
      }
      if (/\/(entries|series)\/\d+$/.test(path)) return HttpResponse.json(resource);
      if (path.includes('leave-types'))
        return HttpResponse.json([
          { id: 9, name: 'Annual leave', effectiveDateUtc: '2020-01-01T00:00:00Z', expiryDateUtc: null },
        ]);
      if (path.includes('assignment-definitions'))
        return HttpResponse.json([
          {
            id: 100,
            name: 'Coverage',
            locationId: 1,
            categoryId: 10,
            subCategoryId: 11,
            effectiveDateUtc: '2020-01-01T00:00:00Z',
            defaultCapacity: 1,
          },
        ]);
      if (path.includes('/users')) return HttpResponse.json([{ id: userId, firstName: 'Alex', lastName: 'Test' }]);
      return HttpResponse.json([]);
    }),
  );
  const app = await createTestApp({
    permissions: Object.values(Permissions),
    featureFlags: { Scheduling: { enabled: true }, Leave: { enabled: true } },
  });
  const locations = useLocationsStore(app.pinia);
  locations.entities = [{ id: 1, name: 'Vancouver', timezone: 'America/Vancouver' }];
  locations.setSelectedLocationId(1);
  const scope = series ? 'series' : 'event';
  const event = {
    id: 'leave',
    type: 'scheduling.leave',
    sourceModule: 'scheduling',
    title: 'Leave',
    start: resource.startAtUtc,
    end: resource.endAtUtc,
    timeZoneId: resource.timeZoneId,
    locationId: 1,
    statusTypeCode: 'Draft',
    resourceIds: [userId],
    metadata: { leaveEntryId: '41', leaveSeriesId: '42', leaveTypeId: 9, userIds: [userId] },
  } as CalendarEventBase;
  const wrapper =
    kind === 'assignment'
      ? mount(AssignmentModal, {
          props: {
            mode: edit ? 'edit' : 'create',
            editScope: scope,
            assignmentEntryId: edit ? 41 : undefined,
            timeZone: resource.timeZoneId,
            initialDate: '2026-07-13',
          },
          global: { plugins: app.mountPlugins },
          attachTo: document.body,
        })
      : edit
        ? mount(LeaveModal, { props: { event }, global: { plugins: app.mountPlugins }, attachTo: document.body })
        : mount(AddResourceModal, {
            props: { initialDate: '2026-07-13', timeZone: resource.timeZoneId },
            global: { plugins: app.mountPlugins },
            attachTo: document.body,
          });
  mounted.push(wrapper);
  await flushPromises();
  const vm = wrapper.vm as unknown as {
    formData: Record<string, unknown>;
    leaveFormData: Record<string, unknown>;
    editFormData: Record<string, unknown>;
    selectOpenScope: (value: 'event' | 'series') => Promise<void>;
    selectTab: (value: 'edit') => void;
    handleSave: () => Promise<void>;
    handleSaveLeave: () => Promise<void>;
    handleConflictOverride: (note: string) => Promise<void>;
    pendingConflict: unknown;
    conflictRetryError: string;
  };
  if (edit && kind === 'leave') {
    await vm.selectOpenScope(scope);
    vm.selectTab('edit');
  }
  const form = edit && kind === 'leave' ? vm.editFormData : kind === 'leave' ? vm.leaveFormData : vm.formData;
  Object.assign(form, {
    date: '2026-07-13',
    startTime: '09:00',
    endTime: '17:00',
    notes: 'Keep my edits',
    repeatMode: series ? 'custom' : 'never',
    recurrenceRule: series ? 'FREQ=DAILY;COUNT=2' : null,
    ...(kind === 'leave' ? { userIds: [userId], leaveType: 9 } : { locationId: 1, title: 'Coverage', color: 'white' }),
    ...(kind === 'shift' ? { userIds: [userId], publish: 'yes', cancel: 'no' } : {}),
    ...(kind === 'assignment' ? { assignmentDefinitionId: 100, categoryId: 10, subCategoryId: 11, capacity: 1 } : {}),
  });
  await flushPromises();
  const originalOffset = IANAZone.prototype.offset;
  const browser = { winterOffset: -480 };
  vi.spyOn(IANAZone.prototype, 'offset').mockImplementation(function (this: IANAZone, instant) {
    return instant === Date.UTC(2027, 0, 15, 12) ? browser.winterOffset : originalOffset.call(this, instant);
  });
  return {
    wrapper,
    vm,
    writes,
    browser,
    store: useCalendarStore(app.pinia),
    get form() {
      return edit && kind === 'leave' ? vm.editFormData : kind === 'leave' ? vm.leaveFormData : vm.formData;
    },
    save: () => (kind === 'leave' && !edit ? vm.handleSaveLeave() : vm.handleSave()),
  };
}

function createConflict(overlapEventId = '2', resourceId = userId): CalendarConflictResponse {
  const entry = {
    eventId: '1',
    sourceModule: 'scheduling',
    sourceEntityId: 41,
    title: 'Coverage',
    start: '2026-07-13T16:00:00Z',
    end: '2026-07-14T00:00:00Z',
    timeZoneId: 'America/Vancouver',
  };
  return {
    id: `1:${overlapEventId}:${resourceId}`,
    entry,
    overlaps: { ...entry, eventId: overlapEventId, sourceEntityId: 99, title: 'Overlap' },
    resourceId,
    overlapStart: entry.start,
    overlapEnd: entry.end,
    isOverridden: false,
    overrideNote: null,
    createdById: null,
    createdOn: null,
    updatedById: null,
    updatedOn: null,
  };
}

describe('blocked scheduling modal saves', () => {
  it.each([
    { kind: 'shift', series: false },
    { kind: 'shift', series: true },
    { kind: 'assignment', series: false },
    { kind: 'assignment', series: true },
    { kind: 'leave', series: false },
    { kind: 'leave', series: true },
    { kind: 'leave', series: false, edit: true },
    { kind: 'leave', series: true, edit: true },
  ] as const)(
    'keeps $kind edits and prevents partial writes (series=$series, edit=$edit)',
    async ({ kind, series, ...options }) => {
      const test = await prepareModal(kind, series, 'edit' in options && options.edit);
      const initialRefresh = test.store.refreshNonce;
      await test.save();
      await flushPromises();
      expect(document.body.textContent).toContain('No changes were saved.');
      expect(test.form.notes).toBe('Keep my edits');
      expect(test.writes).toEqual([]);
      expect(test.store.refreshNonce).toBe(initialRefresh);
      expect(test.wrapper.emitted('close')).toBeUndefined();

      test.browser.winterOffset = -420;
      await test.save();
      await flushPromises();
      expect(test.writes[0]?.body.notes).toBe('Keep my edits');
      expect(test.wrapper.emitted('close')).toBeTruthy();
      expect(test.store.refreshNonce).toBeGreaterThan(initialRefresh);
      if (kind === 'shift') expect(test.writes[1]?.path).toContain('/publish');
    },
  );

  it('guards conflict-override retries while retaining the pending assignment and version', async () => {
    const test = await prepareModal('assignment', false, true);
    const requests: Record<string, unknown>[] = [];
    server.use(
      http.put('*/api/scheduling/assignments/entries/41', async ({ request }) => {
        requests.push((await request.json()) as Record<string, unknown>);
        if (requests.length === 1)
          return HttpResponse.json(
            {
              message: 'Conflict',
              conflicts: [createConflict()],
            },
            { status: 409 },
          );
        return HttpResponse.json({ id: 41, concurrencyToken: 8 });
      }),
    );
    test.browser.winterOffset = -420;
    await test.save();
    expect(test.vm.pendingConflict).toBeTruthy();
    test.browser.winterOffset = -480;
    await test.vm.handleConflictOverride('Approved');
    expect(requests).toHaveLength(1);
    expect(test.vm.conflictRetryError).toContain('No changes were saved.');
    expect(test.form.notes).toBe('Keep my edits');
    expect(test.wrapper.emitted('close')).toBeUndefined();
    test.browser.winterOffset = -420;
    await test.vm.handleConflictOverride('Updated approval');
    expect(requests).toHaveLength(2);
    expect(requests[1]?.concurrencyToken).toBe(7);
    expect(requests[1]?.conflictOverrides).toEqual([
      {
        firstSourceModule: 'scheduling',
        firstEventId: '1',
        secondSourceModule: 'scheduling',
        secondEventId: '2',
        resourceId: userId,
        note: 'Updated approval',
      },
    ]);
    expect(test.wrapper.emitted('close')).toBeTruthy();
  });

  it.each(['event', 'resource', 'sourceModule'] as const)(
    'preserves a distinct conflict by %s and replaces retry notes even when participants are reversed',
    async (distinctBy) => {
      const test = await prepareModal('assignment', false, true);
      const secondConflict = createConflict(
        distinctBy === 'event' ? '3' : '2',
        distinctBy === 'resource' ? 'a1111111-1111-4111-8111-111111111111' : userId,
      );
      if (distinctBy === 'sourceModule') secondConflict.overlaps.sourceModule = 'Scheduling';
      const reversedConflict = {
        ...secondConflict,
        entry: secondConflict.overlaps,
        overlaps: secondConflict.entry,
        resourceId: secondConflict.resourceId.toUpperCase(),
      };
      const requests: Record<string, unknown>[] = [];
      server.use(
        http.put('*/api/scheduling/assignments/entries/41', async ({ request }) => {
          requests.push((await request.json()) as Record<string, unknown>);
          const conflict = [createConflict(), secondConflict, reversedConflict][requests.length - 1];
          return conflict
            ? HttpResponse.json({ message: 'Conflict', conflicts: [conflict] }, { status: 409 })
            : HttpResponse.json({ id: 41, concurrencyToken: 8 });
        }),
      );
      test.browser.winterOffset = -420;
      await test.save();
      await test.vm.handleConflictOverride('First approval');
      test.browser.winterOffset = -480;
      await test.vm.handleConflictOverride('Old second approval');
      expect(requests).toHaveLength(2);
      expect(test.vm.conflictRetryError).toContain('No changes were saved.');
      test.browser.winterOffset = -420;
      await test.vm.handleConflictOverride('Updated second approval');
      await test.vm.handleConflictOverride('Final second approval');
      expect(requests).toHaveLength(4);
      expect(requests[3]?.concurrencyToken).toBe(7);
      expect(requests[3]?.conflictOverrides).toEqual([
        {
          firstSourceModule: 'scheduling',
          firstEventId: '1',
          secondSourceModule: 'scheduling',
          secondEventId: '2',
          resourceId: userId,
          note: 'First approval',
        },
        {
          firstSourceModule: secondConflict.overlaps.sourceModule,
          firstEventId: secondConflict.overlaps.eventId,
          secondSourceModule: secondConflict.entry.sourceModule,
          secondEventId: secondConflict.entry.eventId,
          resourceId: reversedConflict.resourceId,
          note: 'Final second approval',
        },
      ]);
      expect(test.wrapper.emitted('close')).toBeTruthy();
    },
  );
});
