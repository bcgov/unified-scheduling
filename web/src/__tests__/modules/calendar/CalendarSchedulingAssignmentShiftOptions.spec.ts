import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { Permissions, type ShiftEntryResponse } from '@/api-access/generated/models';
import CalendarSchedulingAssignmentModal from '@/modules/scheduling/CalendarSchedulingAssignmentModal.vue';
import type { AssignmentFormData } from '@/modules/scheduling/calendarSchedulingAssignmentForm';
import { useLocationsStore } from '@/stores/LocationsStore';

enableAutoUnmount(afterEach);

const shiftEntry: ShiftEntryResponse = {
  id: 42,
  shiftSeriesId: 210,
  title: 'Persisted shift',
  startAtUtc: '2026-07-13T13:00:00Z',
  endAtUtc: '2026-07-13T14:00:00Z',
  timeZoneId: 'America/Vancouver',
  locationId: 12,
  statusTypeCode: 'Draft',
  userIds: [],
};

async function mountShiftOptions(entries: ShiftEntryResponse[]) {
  const entryQueries: URLSearchParams[] = [];
  const seriesQueries: URLSearchParams[] = [];
  server.use(
    http.get('*/api/users', () => HttpResponse.json([])),
    http.get('*/api/scheduling/assignment-definitions', () => HttpResponse.json([])),
    http.get('*/api/scheduling/shifts/series', ({ request }) => {
      seriesQueries.push(new URL(request.url).searchParams);
      return HttpResponse.json([
        {
          id: 210,
          title: 'Daily shift series',
          startAtUtc: shiftEntry.startAtUtc,
          endAtUtc: shiftEntry.endAtUtc,
          timeZoneId: shiftEntry.timeZoneId,
          locationId: 12,
          statusTypeCode: 'Draft',
          recurrenceRule: 'RRULE:FREQ=DAILY;COUNT=20',
          shiftEntryIds: entries.filter((entry) => entry.shiftSeriesId === 210).map((entry) => entry.id),
          eventIds: entries.filter((entry) => entry.shiftSeriesId === 210).map((entry) => entry.id),
          userIds: [],
        },
      ]);
    }),
    http.get('*/api/scheduling/shifts/entries', ({ request }) => {
      const params = new URL(request.url).searchParams;
      entryQueries.push(params);
      const start = params.get('StartAtUtc')!;
      const end = params.get('EndAtUtc')!;
      // Respect the API's interval query so later matches must actually be loaded.
      return HttpResponse.json(
        entries.filter((entry) => (entry.endAtUtc ?? entry.startAtUtc!) > start && entry.startAtUtc! < end),
      );
    }),
  );

  const app = await createTestApp({
    loadConfig: false,
    permissions: [Permissions.AssignmentsCreate, Permissions.ShiftsView],
  });
  useLocationsStore(app.pinia).setSelectedLocationId(12);
  const wrapper = mount(CalendarSchedulingAssignmentModal, {
    props: { initialDate: '2026-07-13', initialShiftEntryIds: [], timeZone: 'America/Vancouver' },
    global: { plugins: app.mountPlugins },
  });
  const vm = wrapper.vm as unknown as {
    formData: AssignmentFormData;
    isLoadingShiftOptions: boolean;
    shiftSeriesOptions: Array<{ code: number }>;
    shiftEntryOptions: Array<{ code: number }>;
  };
  await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
  return { vm, entryQueries, seriesQueries };
}

describe('assignment shift link options', () => {
  it.each<{ name: string; entries: ShiftEntryResponse[]; included: boolean }>([
    { name: 'excludes a series without persisted entries', entries: [], included: false },
    {
      name: 'excludes an empty series when another series has a matching entry',
      entries: [{ ...shiftEntry, shiftSeriesId: 211 }],
      included: false,
    },
    {
      name: 'excludes entries on dates outside the assignment recurrence',
      entries: [{ ...shiftEntry, startAtUtc: '2026-07-14T13:00:00Z', endAtUtc: '2026-07-14T14:00:00Z' }],
      included: false,
    },
    { name: 'includes a persisted date match without time overlap', entries: [shiftEntry], included: true },
    {
      name: 'includes a later occurrence with a different recurrence pattern',
      entries: [{ ...shiftEntry, startAtUtc: '2026-07-27T13:00:00Z', endAtUtc: '2026-07-27T14:00:00Z' }],
      included: true,
    },
    {
      name: 'excludes cancelled entries',
      entries: [{ ...shiftEntry, statusTypeCode: 'Cancelled' }],
      included: false,
    },
    {
      name: 'excludes away exceptions',
      entries: [{ ...shiftEntry, isException: true, exceptionReasonCodes: ['IsAway'] }],
      included: false,
    },
    {
      name: 'includes time exceptions',
      entries: [{ ...shiftEntry, isException: true, exceptionReasonCodes: ['DateTimeChanged'] }],
      included: true,
    },
    {
      name: 'excludes overlapping time and away exceptions',
      entries: [{ ...shiftEntry, exceptionReasonCodes: ['DateTimeChanged', 'IsAway'] }],
      included: false,
    },
    {
      name: 'includes a match on the shift end date at midnight',
      entries: [{ ...shiftEntry, startAtUtc: '2026-07-13T03:00:00Z', endAtUtc: '2026-07-13T07:00:00Z' }],
      included: true,
    },
    {
      name: 'excludes the middle date of a multi-day shift',
      entries: [{ ...shiftEntry, startAtUtc: '2026-07-11T16:00:00Z', endAtUtc: '2026-07-15T00:00:00Z' }],
      included: false,
    },
  ])('$name', async ({ entries, included }) => {
    const { vm, entryQueries, seriesQueries } = await mountShiftOptions(entries);
    vm.formData.repeatMode = 'custom';
    vm.formData.recurrenceRule = 'RRULE:FREQ=WEEKLY;COUNT=3';
    await flushPromises();
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));

    expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual(included ? [210] : []);
    for (const params of [entryQueries.at(-1)!, seriesQueries.at(-1)!]) {
      expect(params.get('LocationId')).toBe('12');
      expect(params.get('StartAtUtc')).toBe('2026-07-12T07:00:00Z');
      expect(params.get('EndAtUtc')).toBe('2026-07-29T07:00:00Z');
    }
  });

  it('matches the assignment start date in the shift timezone and reloads after recurrence edits', async () => {
    const { vm, entryQueries } = await mountShiftOptions([
      {
        ...shiftEntry,
        startAtUtc: '2026-07-21T00:00:00Z',
        endAtUtc: '2026-07-21T01:00:00Z',
        timeZoneId: 'UTC',
      },
    ]);
    vm.formData.startTime = '18:00';
    vm.formData.endTime = '19:00';
    vm.formData.repeatMode = 'custom';
    vm.formData.recurrenceRule = 'RRULE:FREQ=WEEKLY;COUNT=1';
    await flushPromises();
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    expect(vm.shiftSeriesOptions).toEqual([]);

    vm.formData.recurrenceRule = 'RRULE:FREQ=WEEKLY;COUNT=2';
    await flushPromises();
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    expect(entryQueries.at(-1)!.get('EndAtUtc')).toBe('2026-07-22T07:00:00Z');
    expect(vm.shiftSeriesOptions.map((option) => option.code)).toEqual([210]);

    vm.formData.startTime = '09:00';
    vm.formData.endTime = '17:00';
    await flushPromises();
    await vi.waitFor(() => expect(vm.isLoadingShiftOptions).toBe(false));
    expect(vm.shiftSeriesOptions).toEqual([]);
  });

  it('preserves single-shift selection and its one-day query', async () => {
    const { vm, entryQueries } = await mountShiftOptions([
      shiftEntry,
      { ...shiftEntry, id: 43, startAtUtc: '2026-07-14T13:00:00Z', endAtUtc: '2026-07-14T14:00:00Z' },
      { ...shiftEntry, id: 44, statusTypeCode: 'Cancelled' },
    ]);
    expect(vm.shiftEntryOptions.map((option) => option.code)).toEqual([42]);
    expect(entryQueries.at(-1)!.get('StartAtUtc')).toBe('2026-07-13T07:00:00Z');
    expect(entryQueries.at(-1)!.get('EndAtUtc')).toBe('2026-07-14T07:00:00Z');
  });
});
