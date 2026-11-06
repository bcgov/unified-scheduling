import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
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
  props: ['modelValue'],
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
};

async function mountModal(scope: 'event' | 'series', selectedEntry = entry) {
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
  it('loads the current entry before enabling an event edit', async () => {
    let saved: unknown;
    server.use(
      http.get('*/api/scheduling/leave/entries/42', () => HttpResponse.json(entry)),
      http.put('*/api/scheduling/leave/entries/42', async ({ request }) => {
        saved = await request.json();
        return HttpResponse.json(entry);
      }),
    );
    const wrapper = await mountModal('event', { ...entry, notes: 'Stale note' });
    await vi.waitFor(() => expect(wrapper.get('[data-test="form"]').text()).toContain('Latest server note'));
    expect(wrapper.get('[data-test="form"]').text()).not.toContain('Stale note');

    await wrapper
      .findAll('button')
      .find((button) => button.text() === 'Save')!
      .trigger('click');
    await vi.waitFor(() =>
      expect(saved).toMatchObject({ notes: 'Latest server note', leaveTypeId: 9, leaveSeriesId: 77 }),
    );
    wrapper.unmount();
  });

  it('preserves the timed parent date, hours and recurrence when editing notes through an all-day exception', async () => {
    const series = {
      ...entry,
      id: 77,
      allDay: false,
      leaveDate: null,
      startAtUtc: '2026-06-02T02:00:00Z',
      endAtUtc: '2026-06-02T04:00:00Z',
      recurrenceRule: 'FREQ=WEEKLY;COUNT=4',
    };
    const exception = { ...entry, allDay: true, leaveDate: '2026-06-15' };
    let saved: unknown;
    server.use(
      http.get('*/api/scheduling/leave/series/77', () => HttpResponse.json(series)),
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
    wrapper.unmount();
  });
});
