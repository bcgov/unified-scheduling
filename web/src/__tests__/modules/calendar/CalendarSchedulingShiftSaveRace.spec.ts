import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { server } from '@/__tests__/mocks/server';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { Permissions } from '@/api-access/generated/models';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import ShiftModal from '@/modules/scheduling/CalendarSchedulingShiftDetailModal.vue';

const userId = '11111111-1111-4111-8111-111111111111';
const wrappers: VueWrapper[] = [];
const resource = (id = 41, concurrencyToken = 7) => ({
  id, concurrencyToken, title: `Resource ${id}`, statusTypeCode: 'Draft',
  startAtUtc: '2026-07-13T09:00:00Z', endAtUtc: '2026-07-13T17:00:00Z',
  timeZoneId: 'UTC', locationId: 1, userIds: [userId], recurrenceRule: 'FREQ=DAILY;COUNT=2',
});
const event = (id = 41) => ({
  id: `shift-${id}`, type: 'scheduling.shift', sourceModule: 'scheduling', title: 'Shift',
  start: '2026-07-13T09:00:00Z', end: '2026-07-13T17:00:00Z', timeZoneId: 'UTC',
  locationId: 1, statusTypeCode: 'Draft', resourceIds: [userId],
  metadata: { shiftEntryId: String(id), shiftSeriesId: '42', userIds: [userId] },
}) as CalendarEventBase;

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((complete) => { resolve = complete; });
  return { promise, resolve };
}

async function setup(scope: 'entry' | 'series' = 'entry') {
  const path = `/api/scheduling/shifts/${scope === 'entry' ? 'entries/41' : 'series/42'}`;
  const saveStarted = deferred<void>();
  const saveResponse = deferred<Response>();
  server.use(
    http.get('*/api/*', ({ request }) => {
      const pathname = new URL(request.url).pathname;
      return HttpResponse.json(pathname === path ? resource() : pathname.endsWith('/entries/43') ? resource(43, 11) : []);
    }),
    http.put(`*${path}`, async () => { saveStarted.resolve(); return saveResponse.promise; }),
  );
  const app = await createTestApp({ loadConfig: false, permissions: [Permissions.ShiftsEdit] });
  const refresh = vi.spyOn(useCalendarStore(app.pinia), 'refresh');
  const wrapper = mount(ShiftModal, {
    props: { event: event(), initialOpenScope: scope === 'entry' ? 'event' : 'series' },
    global: { plugins: app.mountPlugins }, attachTo: document.body,
  });
  wrappers.push(wrapper);
  const vm = wrapper.vm as unknown as {
    selectOpenScope: (scope: 'event' | 'series') => Promise<void>;
    selectTab: (tab: 'edit') => void;
    handleSaveEdit: () => Promise<void>;
    loadSelectedEntry: () => Promise<unknown>;
    loadSelectedSeries: () => Promise<unknown>;
    concurrencyToken: number | null;
    isSaving: boolean;
    editFormData: { title: string };
  };
  await vm.selectOpenScope(scope === 'entry' ? 'event' : 'series');
  vm.selectTab('edit');
  await flushPromises();
  return { wrapper, vm, path, refresh, saveStarted, saveResponse };
}

afterEach(() => {
  wrappers.splice(0).forEach((wrapper) => wrapper.unmount());
  document.body.innerHTML = '';
});

describe('shift save response ownership', () => {
  it.each(['entry', 'series'] as const)('keeps a successful %s save when a newer GET is pending', async (scope) => {
    const { wrapper, vm, path, refresh, saveStarted, saveResponse } = await setup(scope);
    const saving = vm.handleSaveEdit();
    await saveStarted.promise;
    const getStarted = deferred<void>();
    const getResponse = deferred<Response>();
    server.use(http.get(`*${path}`, async () => { getStarted.resolve(); return getResponse.promise; }));
    const loading = scope === 'entry' ? vm.loadSelectedEntry() : vm.loadSelectedSeries();
    await getStarted.promise;

    saveResponse.resolve(HttpResponse.json(resource(41, 8)));
    await saving;

    expect(wrapper.emitted('close')).toHaveLength(1);
    expect(refresh).toHaveBeenCalledOnce();
    getResponse.resolve(HttpResponse.json(resource()));
    await loading;
    expect(vm.concurrencyToken).toBeNull(); // Closing invalidated the pending GET.
  });

  it('refreshes after an old save without overwriting or closing the newly opened resource', async () => {
    const { wrapper, vm, refresh, saveStarted, saveResponse } = await setup();
    const saving = vm.handleSaveEdit();
    await saveStarted.promise;
    await wrapper.setProps({ event: event(43) });
    await vm.loadSelectedEntry();
    expect(vm.concurrencyToken).toBe(11);

    saveResponse.resolve(HttpResponse.json(resource(41, 8)));
    await saving;

    expect(vm.concurrencyToken).toBe(11);
    expect(vm.editFormData.title).toBe('Resource 43');
    expect(wrapper.emitted('close')).toBeUndefined();
    expect(refresh).toHaveBeenCalledOnce();
  });

  it('does not update component state after unmount but refreshes a completed save', async () => {
    const { wrapper, vm, refresh, saveStarted, saveResponse } = await setup();
    const saving = vm.handleSaveEdit();
    await saveStarted.promise;
    wrapper.unmount();
    saveResponse.resolve(HttpResponse.json(resource(41, 8)));
    await saving;

    expect(vm.concurrencyToken).toBe(7);
    expect(vm.isSaving).toBe(true);
    expect(wrapper.emitted('close')).toBeUndefined();
    expect(refresh).toHaveBeenCalledOnce();
  });

  it('retains normal successful save refresh and close behaviour', async () => {
    const { wrapper, vm, refresh, saveStarted, saveResponse } = await setup();
    const saving = vm.handleSaveEdit();
    await saveStarted.promise;
    saveResponse.resolve(HttpResponse.json(resource(41, 8)));
    await saving;

    expect(wrapper.emitted('close')).toHaveLength(1);
    expect(refresh).toHaveBeenCalledOnce();
    expect(vm.isSaving).toBe(false);
  });
});
