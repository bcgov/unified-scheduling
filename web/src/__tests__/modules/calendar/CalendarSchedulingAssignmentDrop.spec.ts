import { describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions } from '@/api-access/generated/models';
import { useLocationsStore } from '@/stores/LocationsStore';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';

const draggedUserId = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const existingUserId = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';
const otherShiftUserId = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';

describe('assignment drop editing', () => {
  it.each(['Draft', 'Published'])(
    'preserves loaded values and enforces lifecycle for %s assignments',
    async (statusTypeCode) => {
      let releaseDefinitions!: () => void;
      const definitionsReady = new Promise<void>((resolve) => {
        releaseDefinitions = resolve;
      });
      let assignmentLoaded!: () => void;
      const loaded = new Promise<void>((resolve) => {
        assignmentLoaded = resolve;
      });
      const updates: unknown[] = [];
      const assignment = {
        id: 257,
        assignmentDefinitionId: 7,
        assignmentSeriesId: 211,
        title: 'Custom assignment',
        description: 'Keep description',
        notes: 'Keep notes',
        color: '#123456',
        startAtUtc: '2026-07-13T18:00:00Z',
        endAtUtc: '2026-07-13T22:00:00Z',
        seriesStartAtUtc: '2026-07-01T18:00:00Z',
        seriesEndAtUtc: '2026-08-01T22:00:00Z',
        timeZoneId: 'America/Vancouver',
        allDay: false,
        locationId: 12,
        categoryId: 10,
        subCategoryId: 20,
        capacity: 4,
        statusTypeCode,
        assignmentLinks: [
          { id: 301, shiftEntryId: 44, assignedUserIds: [existingUserId] },
          { id: 302, shiftEntryId: 45, assignedUserIds: [otherShiftUserId] },
        ],
      };
      server.use(
        http.get('*/api/users', () =>
          HttpResponse.json([
            { id: draggedUserId, firstName: 'Alex', lastName: 'Alpha' },
            { id: existingUserId, firstName: 'Blair', lastName: 'Beta' },
            { id: otherShiftUserId, firstName: 'Casey', lastName: 'Gamma' },
          ]),
        ),
        http.get('*/api/scheduling/assignment-definitions', async () => {
          await definitionsReady;
          return HttpResponse.json([
            {
              id: 7,
              name: 'Definition defaults',
              locationId: 12,
              categoryId: 30,
              subCategoryId: 40,
              defaultCapacity: 1,
              defaultStartTime: '08:00',
              defaultEndTime: '16:00',
              color: '#abcdef',
              effectiveDateUtc: '2026-07-01T00:00:00Z',
            },
          ]);
        }),
        http.get('*/api/scheduling/shifts/series', () => HttpResponse.json([])),
        http.get('*/api/scheduling/shifts/entries', () =>
          HttpResponse.json([
            {
              id: 44,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              locationId: 12,
              statusTypeCode: 'Draft',
              userIds: [draggedUserId, existingUserId, otherShiftUserId],
            },
            {
              id: 45,
              startAtUtc: '2026-07-13T16:00:00Z',
              endAtUtc: '2026-07-14T00:00:00Z',
              locationId: 12,
              statusTypeCode: 'Draft',
              userIds: [otherShiftUserId],
            },
          ]),
        ),
        http.get('*/api/scheduling/assignments/entries/257', () => {
          assignmentLoaded();
          return HttpResponse.json(assignment);
        }),
        http.put('*/api/scheduling/assignments/entries/257', async ({ request }) => {
          updates.push(await request.json());
          return HttpResponse.json(assignment);
        }),
      );
      const app = await createTestApp({ loadConfig: false, permissions: Object.values(Permissions) });
      useLocationsStore(app.pinia).setSelectedLocationId(12);
      const wrapper = mount(CalendarSchedulingAssignmentModal, {
        props: {
          mode: 'edit',
          editScope: 'event',
          assignmentEntryId: 257,
          assignmentSeriesId: 211,
          initialDate: '2026-07-13',
          initialAssignmentDefinitionId: 7,
          initialShiftEntryIds: [44],
          initialAssignedUserId: draggedUserId,
          draggedUserOnly: true,
          timeZone: 'America/Vancouver',
        },
        global: { plugins: app.mountPlugins },
        attachTo: document.body,
      });
      try {
        await loaded;
        await flushPromises();
        const vm = wrapper.vm as unknown as {
          formData: { capacity?: number; startTime?: string; endTime?: string };
          handleSave(): Promise<void>;
        };
        expect(vm.formData.capacity).toBe(4);
        releaseDefinitions();
        await flushPromises();
        expect(vm.formData.startTime).toBe('11:00');
        expect(vm.formData.endTime).toBe('15:00');
        await vm.handleSave();
        if (statusTypeCode === 'Published') {
          expect(document.body.textContent).toContain('Assignment Details');
          expect(
            Array.from(document.body.querySelectorAll('button')).some(
              (button) => button.textContent?.trim() === 'Save',
            ),
          ).toBe(false);
          expect(updates).toEqual([]);
        } else {
          expect(updates).toEqual([
            {
              assignmentDefinitionId: 7,
              assignmentSeriesId: 211,
              title: assignment.title,
              description: assignment.description,
              notes: assignment.notes,
              color: assignment.color,
              startAtUtc: assignment.startAtUtc,
              endAtUtc: assignment.endAtUtc,
              seriesStartAtUtc: assignment.seriesStartAtUtc,
              seriesEndAtUtc: assignment.seriesEndAtUtc,
              timeZoneId: assignment.timeZoneId,
              allDay: false,
              locationId: 12,
              categoryId: 10,
              subCategoryId: 20,
              capacity: 4,
              shiftEntryLinks: [
                { shiftEntryId: 44, assignedUserIds: [existingUserId, draggedUserId] },
                { shiftEntryId: 45, assignedUserIds: [otherShiftUserId] },
              ],
            },
          ]);
        }
      } finally {
        releaseDefinitions();
        wrapper.unmount();
      }
    },
  );
});
