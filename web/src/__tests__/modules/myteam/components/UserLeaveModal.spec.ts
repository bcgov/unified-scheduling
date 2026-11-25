import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import type { LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import UserLeaveModal from '@/modules/myteam/components/UserLeaveModal.vue';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { defineComponent } from 'vue';
import { describe, expect, it, vi } from 'vitest';

const UaModalStub = defineComponent({
  name: 'UaModal',
  template: '<div><slot /><slot name="actions" /></div>',
});

const UaBtnStub = defineComponent({
  name: 'UaBtn',
  emits: ['click'],
  template: '<button @click="$emit(\'click\')"><slot /></button>',
});

const LeaveFormStub = defineComponent({
  name: 'LeaveFormComponent',
  props: ['modelValue', 'recurrenceLocked'],
  emits: ['update:modelValue'],
  template: '<div data-test="form">{{ JSON.stringify(modelValue) }}</div>',
});

const entry = {
  id: 42,
  title: 'Leave',
  userIds: ['current-user'],
  leaveTypeId: 9,
  startAtUtc: '2026-06-10T17:00:00Z',
  endAtUtc: '2026-06-10T18:00:00Z',
  timeZoneId: 'America/Vancouver',
  notes: 'Latest server note',
  locationId: 3,
  leaveSeriesId: 77,
  statusTypeCode: 'Draft',
};

async function mountModal(
  scope?: 'event' | 'series',
  selectedEntry: (Omit<typeof entry, 'endAtUtc'> & { endAtUtc: string | null }) | null = entry,
) {
  const app = await createTestApp({ loadConfig: false });
  return mount(UserLeaveModal, {
    props: {
      user: { id: 'current-user', homeLocationId: 3 } as never,
      scope,
      entry: selectedEntry,
    },
    global: {
      plugins: app.mountPlugins,
      stubs: {
        UaModal: UaModalStub,
        UaBtn: UaBtnStub,
        UaAlert: true,
        LeaveFormComponent: LeaveFormStub,
      },
    },
  });
}

describe('UserLeaveModal', () => {
  it.each([
    { startAtUtc: '2026-06-10T17:00:00Z', endAtUtc: '2026-06-10T18:00:00Z', endTime: '11:00' },
    { startAtUtc: '2026-06-10T17:00:00Z', endAtUtc: null, endTime: null },
    { startAtUtc: '2026-06-11T02:00:00Z', endAtUtc: null, endTime: null },
  ])(
    'preserves the loaded end when saving unrelated changes ($startAtUtc, $endAtUtc)',
    async ({ startAtUtc, endAtUtc, endTime }) => {
      const loadedEntry = { ...entry, startAtUtc, endAtUtc };
      let saved: unknown;
      server.use(
        http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json(loadedEntry)),
        http.put('*/api/scheduling/leave/entries/42', async ({ request }) => {
          saved = await request.json();
          return HttpResponse.json(loadedEntry);
        }),
      );
      const wrapper = await mountModal('event', { ...entry, notes: 'Stale note' });
      await vi.waitFor(() => expect(wrapper.get('[data-test="form"]').text()).toContain('Latest server note'));
      expect(wrapper.get('[data-test="form"]').text()).not.toContain('Stale note');
      const form = wrapper.getComponent(LeaveFormStub);
      const modelValue = form.props('modelValue') as LeaveFormData;
      expect(modelValue.endTime).toBe(endTime);
      form.vm.$emit('update:modelValue', { ...modelValue, notes: 'Edited unrelated notes' });
      await flushPromises();
      await wrapper
        .findAll('button')
        .find((button) => button.text() === 'Save')!
        .trigger('click');
      await vi.waitFor(() =>
        expect(saved).toMatchObject({
          notes: 'Edited unrelated notes',
          leaveTypeId: 9,
          leaveSeriesId: 77,
          startAtUtc,
          endAtUtc,
        }),
      );
      await vi.waitFor(() => expect(wrapper.emitted('save')).toHaveLength(1));
      wrapper.unmount();
    },
  );

  it.each([
    { status: 'Draft', locked: false, startAtUtc: '2026-06-02T02:00:00Z', endAtUtc: '2026-06-02T04:00:00Z' },
    {
      status: 'Active',
      locked: true,
      startAtUtc: '2026-06-02T02:00:30.123456Z',
      endAtUtc: '2026-06-02T04:00:45.654321Z',
    },
  ])(
    'preserves the $status parent schedule when editing notes through an all-day exception',
    async ({ status, locked, startAtUtc, endAtUtc }) => {
      const series = {
        ...entry,
        id: 77,
        statusTypeCode: status,
        leaveEntryIds: [42],
        allDay: false,
        leaveDate: null,
        startAtUtc,
        endAtUtc,
        recurrenceRule: 'FREQ=WEEKLY;COUNT=4',
      };
      const exception = { ...entry, allDay: true, leaveDate: '2026-06-15' };
      let saved: unknown;
      const occurrenceRequests: string[] = [];
      server.use(
        http.get('*/api/scheduling/leave/series/77', () => HttpResponse.json(series)),
        http.get('*/api/scheduling/leave/users/:userId/entries', ({ request }) => {
          occurrenceRequests.push(request.url);
          return HttpResponse.json([]);
        }),
        http.get('*/api/scheduling/leave/entries/:id', ({ request }) => {
          occurrenceRequests.push(request.url);
          return HttpResponse.json(entry);
        }),
        http.put('*/api/scheduling/leave/series/77', async ({ request }) => {
          saved = await request.json();
          return HttpResponse.json(series);
        }),
      );
      const wrapper = await mountModal('series', exception);
      const form = wrapper.getComponent(LeaveFormStub);
      await vi.waitFor(() =>
        expect(form.props('modelValue')).toMatchObject({
          date: '2026-06-01',
          allDay: false,
          startTime: '19:00',
          endTime: '21:00',
          recurrenceRule: series.recurrenceRule,
        }),
      );
      expect(form.props('recurrenceLocked')).toBe(locked);
      form.vm.$emit('update:modelValue', { ...form.props('modelValue'), notes: 'Updated notes' });
      await flushPromises();
      await wrapper
        .findAll('button')
        .find((button) => button.text() === 'Save')!
        .trigger('click');

      await vi.waitFor(() =>
        expect(saved).toMatchObject({
          allDay: false,
          startAtUtc: series.startAtUtc,
          endAtUtc: series.endAtUtc,
          timeZoneId: 'America/Vancouver',
          recurrenceRule: series.recurrenceRule,
          notes: 'Updated notes',
        }),
      );
      expect(saved).not.toHaveProperty('leaveDate');
      expect(occurrenceRequests).toEqual([]);
      wrapper.unmount();
    },
  );

  it('retains default start and end times for new leave creation', async () => {
    let saved: unknown;
    let requestedEntry = false;
    let requestedSeries = false;
    server.use(
      http.get('*/api/scheduling/leave/entries/:id', () => {
        requestedEntry = true;
        return HttpResponse.json(entry);
      }),
      http.get('*/api/scheduling/leave/series/:id', () => {
        requestedSeries = true;
        return HttpResponse.json({});
      }),
      http.post('*/api/scheduling/leave/entries', async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(entry);
      }),
    );
    const wrapper = await mountModal(undefined, null);
    await flushPromises();
    expect(requestedEntry).toBe(false);
    expect(requestedSeries).toBe(false);
    const form = wrapper.getComponent(LeaveFormStub);
    const modelValue = form.props('modelValue') as LeaveFormData;
    expect(modelValue.startTime).toBe('09:00');
    expect(modelValue.endTime).toBe('17:00');
    form.vm.$emit('update:modelValue', { ...modelValue, date: '2026-06-10', leaveType: 9 });
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await vi.waitFor(() =>
      expect(saved).toMatchObject({
        startAtUtc: '2026-06-10T16:00:00Z',
        endAtUtc: '2026-06-11T00:00:00Z',
      }),
    );
    await vi.waitFor(() => expect(wrapper.emitted('save')).toHaveLength(1));
    wrapper.unmount();
  });
});
