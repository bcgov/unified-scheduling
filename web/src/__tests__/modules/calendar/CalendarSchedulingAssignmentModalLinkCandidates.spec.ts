import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions, type AssignmentSeriesResponse } from '@/api-access/generated/models';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import type { AssignmentFormData } from '@/modules/scheduling/calendarSchedulingAssignmentForm';
import { useLocationsStore } from '@/stores/LocationsStore';

enableAutoUnmount(afterEach);

const series: AssignmentSeriesResponse = {
  id: 300,
  title: 'Coverage',
  concurrencyToken: 1,
  assignmentDefinitionId: 7,
  categoryId: 10,
  subCategoryId: 20,
  capacity: 1,
  locationId: 12,
  statusTypeCode: 'Draft',
  startAtUtc: '2026-07-13T16:00:00Z',
  endAtUtc: '2026-07-14T00:00:00Z',
  timeZoneId: 'America/Vancouver',
  allDay: false,
  recurrenceRule: 'FREQ=DAILY;COUNT=3',
};
const shiftEntries = [13, 14, 15].map((day, index) => ({
  id: 42 + index,
  shiftSeriesId: 200 + index,
  title: `Shift ${day}`,
  locationId: 12,
  statusTypeCode: 'Draft',
  startAtUtc: `2026-07-${day}T16:00:00Z`,
  endAtUtc: `2026-07-${day + 1}T00:00:00Z`,
  timeZoneId: 'America/Vancouver',
  userIds: [],
}));
const persistedEntries = [
  { ...shiftEntries[1], id: 101, assignmentSeriesId: 300 },
  { ...shiftEntries[2], id: 102, assignmentSeriesId: 300, statusTypeCode: 'Cancelled' },
];

describe('assignment shift-link candidates', () => {
  let entryQueries: URLSearchParams[];
  let shiftQueries: URLSearchParams[];

  beforeEach(() => {
    entryQueries = [];
    shiftQueries = [];
    server.use(
      http.get('*/api/scheduling/assignment-definitions', () => HttpResponse.json([])),
      http.get('*/api/users', () => HttpResponse.json([])),
      http.get('*/api/scheduling/assignments/series/:id', ({ params }) =>
        HttpResponse.json({ ...series, id: Number(params.id) }),
      ),
      http.get('*/api/scheduling/assignments/entries', ({ request }) => {
        entryQueries.push(new URL(request.url).searchParams);
        return HttpResponse.json(persistedEntries);
      }),
      http.get('*/api/scheduling/shifts/entries', ({ request }) => {
        shiftQueries.push(new URL(request.url).searchParams);
        return HttpResponse.json(shiftEntries);
      }),
      http.get('*/api/scheduling/shifts/series', ({ request }) => {
        shiftQueries.push(new URL(request.url).searchParams);
        return HttpResponse.json(
          shiftEntries.map((entry) => ({ ...entry, id: entry.shiftSeriesId, shiftEntryIds: [entry.id] })),
        );
      }),
    );
  });

  async function mountModal(mode: 'create' | 'edit' = 'edit') {
    const app = await createTestApp({ loadConfig: false, permissions: Object.values(Permissions) });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode,
        assignmentSeriesId: mode === 'edit' ? 300 : undefined,
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: { template: '<div><slot name="alerts" /><slot /><slot name="actions" /></div>' },
          RRuleEditor: true,
        },
      },
    });
    const vm = wrapper.vm as unknown as {
      formData: AssignmentFormData;
      assignmentLoadState: string;
      shiftSeriesOptions: Array<{ code: number }>;
      isLoadingShiftOptions: boolean;
      apiError: string;
      updateField<K extends keyof AssignmentFormData>(key: K, value: AssignmentFormData[K]): void;
    };
    await vi.waitFor(() => expect(vm.assignmentLoadState).toBe('loaded'));
    await flushPromises();
    return { wrapper, vm, locationsStore };
  }

  it('excludes deleted and cancelled occurrences when editing an unchanged series', async () => {
    const { vm } = await mountModal();
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
    expect(entryQueries).toHaveLength(1);
    expect(entryQueries[0]?.get('AssignmentSeriesId')).toBe('300');
  });

  it.each([
    { field: 'recurrenceRule', value: 'FREQ=DAILY;COUNT=4' },
    { field: 'startTime', value: '10:00' },
    { field: 'endTime', value: '18:00' },
    { field: 'date', value: '2026-07-14' },
  ] as const)(
    'uses proposed occurrences when $field changes, and persisted entries when reverted',
    async ({ field, value }) => {
      const { vm } = await mountModal();
      await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
      const previous = vm.formData[field];
      vm.updateField(field, value);
      await vi.waitFor(() =>
        expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual(
          field === 'date' ? [201, 202] : [200, 201, 202],
        ),
      );
      vm.updateField(field, previous);
      await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
    },
  );

  it('uses generated occurrences during creation without loading persisted entries', async () => {
    const { vm } = await mountModal('create');
    vm.updateField('repeatMode', 'custom');
    vm.updateField('recurrenceRule', series.recurrenceRule);
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([200, 201, 202]));
    expect(entryQueries).toHaveLength(0);
  });

  it('uses proposed occurrences and reloads when the timezone changes', async () => {
    const { vm, locationsStore } = await mountModal();
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    locationsStore.entities = [{ id: 12, timezone: 'America/Los_Angeles' }];
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([200, 201, 202]));
    const requestCount = shiftQueries.length;
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    locationsStore.entities = [];
    await vi.waitFor(() => expect(shiftQueries).toHaveLength(requestCount + 2));
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
  });

  it('uses proposed occurrences when saving an all-day series as a timed assignment', async () => {
    server.use(
      http.get('*/api/scheduling/assignments/series/:id', () => HttpResponse.json({ ...series, allDay: true })),
    );
    const { vm } = await mountModal();
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([200, 201, 202]));
  });

  it('does not reload shift options for Notes or capacity, but reloads when query boundaries change', async () => {
    const { vm } = await mountModal();
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    const requestCount = shiftQueries.length;
    vm.updateField('notes', 'Updated notes');
    vm.updateField('capacity', 2);
    await flushPromises();
    expect(shiftQueries).toHaveLength(requestCount);
    expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]);

    vm.updateField('recurrenceRule', 'FREQ=DAILY;COUNT=4');
    await vi.waitFor(() => expect(shiftQueries).toHaveLength(requestCount + 2));
    expect(shiftQueries.at(-1)?.get('EndAtUtc')).toBe('2026-07-18T07:00:00Z');
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    vm.updateField('notes', 'More notes');
    await flushPromises();
    expect(shiftQueries).toHaveLength(requestCount + 2);
  });

  it('leaves candidates empty when persisted entries cannot be loaded', async () => {
    server.use(
      http.get('*/api/scheduling/assignments/entries', () =>
        HttpResponse.json({ message: 'Entries unavailable.' }, { status: 500 }),
      ),
    );
    const { vm } = await mountModal();
    await vi.waitFor(() => expect(vm.apiError).toBe('Entries unavailable.'));
    expect(vm.shiftSeriesOptions).toEqual([]);
    vm.updateField('recurrenceRule', 'FREQ=DAILY;COUNT=4');
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([200, 201, 202]));
  });

  it('hides candidates while loading entries and ignores a response for the previous series', async () => {
    let resolveEntries!: (response: Response) => void;
    const pendingEntries = new Promise<Response>((resolve) => {
      resolveEntries = resolve;
    });
    server.use(
      http.get('*/api/scheduling/assignments/entries', ({ request }) =>
        new URL(request.url).searchParams.get('AssignmentSeriesId') === '300'
          ? pendingEntries
          : HttpResponse.json([{ ...persistedEntries[0], assignmentSeriesId: 301 }]),
      ),
    );
    const { wrapper, vm } = await mountModal();
    expect(vm.shiftSeriesOptions).toEqual([]);
    await wrapper.setProps({ assignmentSeriesId: 301 });
    await vi.waitFor(() => expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]));
    resolveEntries(HttpResponse.json([{ ...persistedEntries[1], statusTypeCode: 'Draft' }]));
    await flushPromises();
    expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([201]);
  });
});
