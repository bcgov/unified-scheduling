import { afterEach, describe, expect, it, vi } from 'vitest';
import { IANAZone } from 'luxon';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import ShiftModal from '@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue';
import AssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';

const userId = '11111111-1111-4111-8111-111111111111';
const mounted: VueWrapper[] = [];

afterEach(() => {
  vi.restoreAllMocks();
  mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  document.body.innerHTML = '';
});

describe('scheduling edit modal concurrency', () => {
  it.each([
    { kind: 'shift', scope: 'entry' },
    { kind: 'shift', scope: 'series' },
    { kind: 'assignment', scope: 'entry' },
    { kind: 'assignment', scope: 'series' },
  ] as const)(
    'preserves $kind $scope edits until an explicit reload and sends the new version',
    async ({ kind, scope }) => {
      const id = scope === 'entry' ? 41 : 42;
      const path = `/api/scheduling/${kind === 'shift' ? 'shifts' : 'assignments'}/${scope === 'entry' ? 'entries' : 'series'}/${id}`;
      let serverVersion = 7;
      const writes: Array<{ concurrencyToken: number; notes: string }> = [];
      const resource = () => ({
        id,
        concurrencyToken: serverVersion,
        title: 'Court coverage',
        notes: serverVersion === 7 ? 'Original notes' : 'Latest server notes',
        color: 'white',
        startAtUtc: '2026-07-13T09:00:00Z',
        endAtUtc: '2026-07-13T17:00:00Z',
        timeZoneId: 'America/Vancouver',
        allDay: false,
        locationId: 1,
        statusTypeCode: 'Draft',
        userIds: kind === 'shift' ? [userId] : [],
        recurrenceRule: scope === 'series' ? 'FREQ=DAILY;COUNT=2' : null,
        assignmentDefinitionId: 100,
        categoryId: 10,
        subCategoryId: 11,
        capacity: 1,
        assignmentLinks: [],
        shiftSeriesLinks: [],
        assignedUserIds: [],
        linkedShiftEntryIds: [],
      });

      server.use(
        http.get('*/api/*', ({ request }) => {
          const requestPath = new URL(request.url).pathname;
          if (requestPath === path) return HttpResponse.json(resource());
          if (requestPath.includes('assignment-definitions')) {
            return HttpResponse.json([
              {
                id: 100,
                name: 'Court coverage',
                locationId: 1,
                categoryId: 10,
                subCategoryId: 11,
                effectiveDateUtc: '2020-01-01T00:00:00Z',
                expiryDateUtc: null,
                defaultCapacity: 1,
              },
            ]);
          }
          if (requestPath.includes('/users')) {
            return HttpResponse.json([{ id: userId, firstName: 'Alex', lastName: 'Test' }]);
          }
          return HttpResponse.json([]);
        }),
        http.put(`*${path}`, async ({ request }) => {
          writes.push((await request.json()) as { concurrencyToken: number; notes: string });
          if (writes.length === 1) {
            serverVersion = 8;
            return HttpResponse.json(
              {
                status: 409,
                code: 'concurrency_conflict',
                title: 'Resource changed',
                detail: 'Reload first.',
              },
              { status: 409 },
            );
          }
          return HttpResponse.json({ ...resource(), concurrencyToken: 9 });
        }),
      );

      const app = await createTestApp({
        loadConfig: false,
        permissions: [Permissions.ShiftsEdit, Permissions.AssignmentsEdit, Permissions.AssignmentsAssign],
      });
      const wrapper =
        kind === 'shift'
          ? mount(ShiftModal, {
              props: {
                initialOpenScope: scope === 'entry' ? 'event' : 'series',
                event: {
                  id: 'shift',
                  type: 'scheduling.shift',
                  sourceModule: 'scheduling',
                  title: 'Summary',
                  start: '2026-07-13T09:00:00Z',
                  end: '2026-07-13T17:00:00Z',
                  timeZoneId: 'America/Vancouver',
                  locationId: 1,
                  statusTypeCode: 'Draft',
                  resourceIds: [userId],
                  metadata: { shiftEntryId: '41', shiftSeriesId: '42', userIds: [userId], concurrencyToken: 999 },
                } as CalendarEventBase,
              },
              global: { plugins: app.mountPlugins },
              attachTo: document.body,
            })
          : mount(AssignmentModal, {
              props: {
                mode: 'edit',
                editScope: scope === 'entry' ? 'event' : 'series',
                assignmentEntryId: 41,
                assignmentSeriesId: 42,
                timeZone: 'America/Vancouver',
              },
              global: { plugins: app.mountPlugins },
              attachTo: document.body,
            });
      mounted.push(wrapper);
      const vm = wrapper.vm as unknown as {
        concurrencyToken: number;
        hasConcurrencyConflict: boolean;
        pendingConflict: unknown;
        editFormData: { notes: string };
        formData: { notes: string };
        loadSelectedEntry: () => Promise<unknown>;
        loadSelectedSeries: () => Promise<unknown>;
        loadInitialAssignment: () => Promise<unknown>;
        loadAssignmentDefinitions: () => Promise<unknown>;
        selectTab: (tab: 'edit') => void;
        selectOpenScope: (scope: 'event' | 'series') => Promise<void>;
        handleSaveEdit: () => Promise<void>;
        handleSave: () => Promise<void>;
        reloadLatest: () => Promise<void>;
      };
      if (kind === 'shift') {
        await vm.selectOpenScope(scope === 'entry' ? 'event' : 'series');
        vm.selectTab('edit');
      } else {
        await vm.loadInitialAssignment();
        await vm.loadAssignmentDefinitions();
      }
      await flushPromises();
      const form = () => (kind === 'shift' ? vm.editFormData : vm.formData);
      const save = () => (kind === 'shift' ? vm.handleSaveEdit() : vm.handleSave());
      // The token comes from the full response, even when a summary contains another value.
      expect(vm.concurrencyToken).toBe(7);
      form().notes = 'Unsaved local notes';

      const originalOffset = IANAZone.prototype.offset;
      let winterOffset = -480;
      vi.spyOn(IANAZone.prototype, 'offset').mockImplementation(function (this: IANAZone, instant) {
        return instant === Date.UTC(2027, 0, 15, 12) ? winterOffset : originalOffset.call(this, instant);
      });
      await save();
      await flushPromises();
      expect(writes).toHaveLength(0);
      expect(form().notes).toBe('Unsaved local notes');
      expect(vm.concurrencyToken).toBe(7);
      expect(vm.hasConcurrencyConflict).toBe(false);
      expect(wrapper.emitted('close')).toBeUndefined();
      expect(document.body.textContent).toContain('No changes were saved.');
      winterOffset = -420;

      await save();
      await flushPromises();

      expect(writes).toHaveLength(1);
      expect(writes[0]).toMatchObject({ concurrencyToken: 7, notes: 'Unsaved local notes' });
      expect(vm.hasConcurrencyConflict).toBe(true);
      expect(form().notes).toBe('Unsaved local notes');
      expect(wrapper.emitted('close')).toBeUndefined();
      expect(document.body.textContent).toContain('Reloading replaces your unsaved changes');
      expect(document.body.textContent).toContain('Reload latest');
      if (kind === 'assignment') expect(vm.pendingConflict).toBeNull();

      await save();
      expect(writes).toHaveLength(1);
      await vm.reloadLatest();
      expect(form().notes).toBe('Latest server notes');
      expect(vm.concurrencyToken).toBe(8);
      expect(vm.hasConcurrencyConflict).toBe(false);

      await save();
      expect(writes).toHaveLength(2);
      expect(writes[1]?.concurrencyToken).toBe(8);
      expect(wrapper.emitted('close')).toBeTruthy();
    },
  );
});
