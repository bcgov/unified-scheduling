import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions, type AssignmentSeriesResponse } from '@/api-access/generated/models';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import type { AssignmentFormData } from '@/modules/scheduling/calendarSchedulingAssignmentForm';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useCalendarStore } from '@/modules/calendar/calendarStore';

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

  async function mountModal(
    mode: 'create' | 'edit' | 'view' = 'edit',
    assignmentEntryId?: number,
    permissions = Object.values(Permissions),
    props?: { initialDate?: string; initialShiftEntryIds?: number[]; assignmentSeriesId?: number },
  ) {
    const app = await createTestApp({ loadConfig: false, permissions });
    const locationsStore = useLocationsStore(app.pinia);
    locationsStore.setSelectedLocationId(12);
    const wrapper = mount(CalendarSchedulingAssignmentModal, {
      props: {
        mode,
        assignmentSeriesId: mode === 'create' ? undefined : 300,
        assignmentEntryId,
        editScope: assignmentEntryId ? 'event' : undefined,
        initialDate: '2026-07-13',
        timeZone: 'America/Vancouver',
        ...props,
      },
      global: {
        plugins: app.mountPlugins,
        stubs: {
          UaModal: {
            template:
              '<div><slot name="alerts" /><slot name="secondary-header" /><slot /><slot name="actions" /></div>',
          },
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
      shiftEntryOptions: Array<{ code: number }>;
      selectTab: (tab: 'edit') => void;
      handleSave: () => Promise<void>;
      updateAssignmentDate: (value: string) => void;
      updateSelectedShiftEntry: (id: number) => void;
      removeShiftEntryLink: (index: number) => void;
      updateField<K extends keyof AssignmentFormData>(key: K, value: AssignmentFormData[K]): void;
    };
    await vi.waitFor(() => expect(vm.assignmentLoadState).toBe('loaded'));
    await flushPromises();
    return { wrapper, vm, locationsStore, calendarStore: useCalendarStore(app.pinia) };
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

  const published = {
    ...series,
    id: 101,
    assignmentSeriesId: series.id,
    statusTypeCode: 'Active',
    assignmentLinks: [],
  };
  const shiftUser = '11111111-1111-4111-8111-111111111111';

  const overnightShift = {
    startAtUtc: '2026-07-13T01:00:00Z',
    endAtUtc: '2026-07-13T09:00:00Z',
    timeZoneId: 'America/Vancouver',
  };
  const midnightShift = { ...overnightShift, endAtUtc: '2026-07-13T07:00:00Z' };

  it.each<{
    name: string;
    status?: 'Draft' | 'Active';
    initialDate?: string;
    shift: typeof overnightShift;
    evening?: boolean;
    canAssign?: boolean;
  }>([
    { name: 'a new assignment on the midnight end date', shift: midnightShift },
    { name: 'a standalone draft on the midnight end date', status: 'Draft', shift: midnightShift },
    { name: 'a published assignment on the midnight end date', status: 'Active', shift: midnightShift },
    {
      name: 'a published assignment on the midnight end date without assignment permission',
      status: 'Active',
      shift: midnightShift,
      canAssign: false,
    },
    { name: 'the local start date of an overnight shift', initialDate: '2026-07-12', shift: overnightShift },
    { name: 'the local end date of an overnight shift', shift: overnightShift },
    {
      name: 'an evening assignment whose start falls on the next date in the shift timezone',
      shift: { startAtUtc: '2026-07-14T08:00:00Z', endAtUtc: '2026-07-14T09:00:00Z', timeZoneId: 'UTC' },
      evening: true,
    },
  ])('filters shift options for $name', async ({ status, initialDate, shift, evening, canAssign = true }) => {
    const candidate = { ...shiftEntries[0], ...shift, statusTypeCode: 'Active', userIds: [shiftUser] };
    const ineligibleDate = initialDate || evening ? '2026-07-13' : '2026-07-14';
    const ineligibleHour = evening ? '16' : '04';
    const ineligible = {
      ...candidate,
      id: 98,
      startAtUtc: `${ineligibleDate}T${ineligibleHour}:00:00Z`,
      endAtUtc: `${ineligibleDate}T${ineligibleHour}:30:00Z`,
      timeZoneId: 'UTC',
    };
    let returnedIds: number[] = [];
    server.use(
      http.get('*/api/scheduling/assignments/entries/101', () =>
        HttpResponse.json({ ...published, statusTypeCode: status, assignmentSeriesId: null }),
      ),
      http.get('*/api/scheduling/shifts/entries', ({ request }) => {
        const params = new URL(request.url).searchParams;
        const start = Date.parse(params.get('StartAtUtc') ?? '');
        const end = Date.parse(params.get('EndAtUtc') ?? '');
        const entries = [candidate, ineligible, { ...candidate, id: 99, statusTypeCode: 'Cancelled' }].filter(
          (entry) => Date.parse(entry.endAtUtc) > start && Date.parse(entry.startAtUtc) < end,
        );
        returnedIds = entries.map((entry) => entry.id);
        return HttpResponse.json(entries);
      }),
    );
    const permissions = Object.values(Permissions).filter(
      (permission) => canAssign || permission !== Permissions.AssignmentsAssign,
    );
    const { vm } = await mountModal(status ? 'edit' : 'create', status ? 101 : undefined, permissions, {
      assignmentSeriesId: undefined,
      initialDate: initialDate ?? '2026-07-13',
      initialShiftEntryIds: evening ? undefined : [98],
    });
    if (evening) {
      vm.updateField('startTime', '18:00');
      vm.updateField('endTime', '19:00');
    }

    await vi.waitFor(() => expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([42]));
    expect(returnedIds).toEqual([42, 98, 99]);
    expect(vm.formData.shiftEntryLinks).toEqual([]);
    vm.updateSelectedShiftEntry(98);
    vm.updateSelectedShiftEntry(99);
    expect(vm.formData.shiftEntryLinks).toEqual([]);
    vm.updateSelectedShiftEntry(42);
    expect(vm.formData.shiftEntryLinks).toEqual(canAssign ? [{ shiftEntryId: 42, assignedUserIds: [shiftUser] }] : []);
  });

  it('saves a published occurrence and a shift link on the midnight end date without publish permission', async () => {
    const update = vi.fn<(_body: unknown) => void>();
    const publish = vi.fn();
    server.use(
      http.get('*/api/scheduling/assignments/entries/101', () => HttpResponse.json(published)),
      http.get('*/api/scheduling/shifts/entries', () =>
        HttpResponse.json([{ ...shiftEntries[0], ...midnightShift, statusTypeCode: 'Active', userIds: [shiftUser] }]),
      ),
      http.put('*/api/scheduling/assignments/entries/101', async ({ request }) => {
        update(await request.json());
        return HttpResponse.json({ ...published, concurrencyToken: 2 });
      }),
      http.post('*/api/scheduling/assignments/entries/101/publish', () => {
        publish();
        return HttpResponse.json(published);
      }),
    );
    const { wrapper, vm, calendarStore } = await mountModal(
      'view',
      101,
      Object.values(Permissions).filter((permission) => permission !== Permissions.SchedulePublish),
    );
    const refresh = vi.spyOn(calendarStore, 'refresh');
    expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['Details', 'Edit', 'Cancel']);
    expect(wrapper.text()).toContain('Saved changes will be applied immediately to the published schedule.');
    vm.selectTab('edit');
    await vi.waitFor(() => expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([42]));
    vm.updateSelectedShiftEntry(42);
    vm.updateField('notes', 'Corrected published assignment');
    await vm.handleSave();

    expect(update).toHaveBeenCalledWith(
      expect.objectContaining({
        concurrencyToken: 1,
        notes: 'Corrected published assignment',
        shiftEntryLinks: [{ shiftEntryId: 42, assignedUserIds: [shiftUser] }],
      }),
    );
    expect(publish).not.toHaveBeenCalled();
    expect(refresh).toHaveBeenCalledTimes(1);
    expect(wrapper.emitted('close')).toHaveLength(1);
  });

  it.each(['Active', 'Cancelled'])(
    'keeps a %s occurrence read-only with the applicable permission missing',
    async (statusTypeCode) => {
      server.use(
        http.get('*/api/scheduling/assignments/entries/101', () => HttpResponse.json({ ...published, statusTypeCode })),
      );
      const { wrapper } = await mountModal(
        'view',
        101,
        statusTypeCode === 'Active'
          ? [Permissions.SchedulePublish, Permissions.AssignmentsExpire]
          : Object.values(Permissions),
      );
      expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(
        statusTypeCode === 'Active' ? ['Details', 'Cancel'] : ['Details'],
      );
    },
  );

  it('keeps a published series read-only with publish permission', async () => {
    server.use(
      http.get('*/api/scheduling/assignments/series/300', () =>
        HttpResponse.json({ ...series, statusTypeCode: 'Active' }),
      ),
    );
    const { wrapper } = await mountModal('view');
    expect(wrapper.findAll('[role="tab"]').map((tab) => tab.text())).toEqual(['Details', 'Cancel']);
  });

  it('omits unavailable links and retains the edited date when a published correction fails', async () => {
    const update = vi.fn<(_body: unknown) => void>();
    server.use(
      http.get('*/api/scheduling/assignments/entries/101', () =>
        HttpResponse.json({
          ...published,
          assignmentLinks: null,
          linkedShiftEntryIds: [42],
          assignedUserIds: [shiftUser],
        }),
      ),
      http.put('*/api/scheduling/assignments/entries/101', async ({ request }) => {
        update(await request.json());
        return HttpResponse.json(
          {
            detail:
              'Remove incompatible shift links or choose an assignment start date that matches the linked shifts.',
          },
          { status: 400 },
        );
      }),
    );
    const { wrapper, vm } = await mountModal('view', 101);
    vm.selectTab('edit');
    vm.updateAssignmentDate('2026-07-14');
    await vm.handleSave();

    expect(update).toHaveBeenCalledTimes(1);
    expect(update.mock.calls[0]?.[0]).not.toHaveProperty('shiftEntryLinks');
    expect(vm.formData.shiftEntryLinks).toBeUndefined();
    expect(vm.formData.date).toBe('2026-07-14');
    expect(vm.apiError).toContain('Remove incompatible shift links');
    expect(wrapper.emitted('close')).toBeUndefined();
  });

  it.each([false, true])(
    'submits an empty replacement only when a user with assignment permission removes the link (%s)',
    async (canAssign) => {
      const update = vi.fn<(_body: unknown) => void>();
      server.use(
        http.get('*/api/scheduling/assignments/entries/101', () =>
          HttpResponse.json({ ...published, assignmentLinks: [{ shiftEntryId: 42, userIds: [shiftUser] }] }),
        ),
        http.put('*/api/scheduling/assignments/entries/101', async ({ request }) => {
          update(await request.json());
          return HttpResponse.json(published);
        }),
      );
      const permissions = Object.values(Permissions).filter(
        (permission) => canAssign || permission !== Permissions.AssignmentsAssign,
      );
      const { vm } = await mountModal('view', 101, permissions);
      vm.selectTab('edit');
      vm.removeShiftEntryLink(0);
      await vm.handleSave();

      expect(update).toHaveBeenCalledTimes(1);
      const body = update.mock.calls[0]?.[0];
      if (canAssign) expect(body).toHaveProperty('shiftEntryLinks', []);
      else expect(body).not.toHaveProperty('shiftEntryLinks');
    },
  );

  it('refreshes after an earlier published save without closing the current assignment', async () => {
    let finishUpdate!: (response: Response) => void;
    const updateResponse = new Promise<Response>((resolve) => {
      finishUpdate = resolve;
    });
    const update = vi.fn();
    server.use(
      http.get('*/api/scheduling/assignments/entries/:id', ({ params }) =>
        HttpResponse.json({ ...published, id: Number(params.id) }),
      ),
      http.put('*/api/scheduling/assignments/entries/101', () => {
        update();
        return updateResponse;
      }),
    );
    const { wrapper, vm, calendarStore } = await mountModal('view', 101);
    const refresh = vi.spyOn(calendarStore, 'refresh');
    vm.selectTab('edit');
    vm.updateField('notes', 'Earlier correction');
    const save = vm.handleSave();
    await vi.waitFor(() => expect(update).toHaveBeenCalledTimes(1));
    await vm.handleSave();
    expect(update).toHaveBeenCalledTimes(1);

    await wrapper.setProps({ assignmentEntryId: 102 });
    finishUpdate(HttpResponse.json({ ...published, concurrencyToken: 2 }));
    await save;
    await vi.waitFor(() => expect(vm.assignmentLoadState).toBe('loaded'));

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(wrapper.emitted('close')).toBeUndefined();
    expect(vm.formData.notes).not.toBe('Earlier correction');
  });
});
