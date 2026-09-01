import { flushPromises, mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { getGetApiUsersIdMockHandler, getGetApiUsersIdResponseMock } from '@/api-access/generated/users/users.msw';
import CalendarConflictDetailModal from '@/modules/calendar/components/CalendarConflictDetailModal.vue';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import { server } from '@/__tests__/mocks/server';

describe('CalendarConflictDetailModal', () => {
  it('displays one conflict and edits either event', async () => {
    const conflict = createConflict(101, 102, 'First assignment', 'Second assignment');
    const wrapper = mountModal(conflict);

    expect(wrapper.text()).toContain('Entry:');
    expect(wrapper.text()).toContain('Overlaps:');
    expect(wrapper.text()).toContain('From:');
    expect(wrapper.text()).toContain('2:00 AM - 4:00 AM');
    expect(wrapper.text()).toContain('3:00 AM - 5:00 AM');

    const editCurrentButtons = wrapper.findAll('button[aria-label="Edit First assignment"]');
    expect(editCurrentButtons).toHaveLength(1);
    await editCurrentButtons[0]?.trigger('click');
    await wrapper.get('button[aria-label="Edit Second assignment"]').trigger('click');

    expect(wrapper.emitted('editEvent')).toEqual([[conflict.entry], [conflict.overlaps]]);
  });

  it('requires override notes and emits the selected conflict with its note', async () => {
    const conflict = createConflict(101, 102, 'First assignment', 'Second assignment');
    const wrapper = mountModal(conflict);
    const overrideButton = wrapper.get('[data-test="ua-button"]');

    expect(overrideButton.attributes('data-color')).toBe('primary');
    expect(wrapper.find('textarea').exists()).toBe(true);
    expect(overrideButton.attributes('disabled')).toBeDefined();
    await wrapper.get('textarea').setValue('Approved coverage overlap');
    expect(overrideButton.attributes('disabled')).toBeUndefined();
    await overrideButton.trigger('click');

    expect(wrapper.emitted('override')).toEqual([['Approved coverage overlap']]);
  });

  it('displays who last updated an existing override', async () => {
    server.use(
      getGetApiUsersIdMockHandler(
        getGetApiUsersIdResponseMock({
          id: 'user-2',
          firstName: 'Taylor',
          lastName: 'Ng',
          idirName: 'tng',
        }),
      ),
    );
    const conflict = createConflict(101, 102, 'First assignment', 'Second assignment');
    conflict.isOverridden = true;
    conflict.createdById = 'user-1';
    conflict.createdOn = '2026-08-11T16:00:00Z';
    conflict.updatedById = 'user-2';
    conflict.updatedOn = '2026-08-11T17:30:00Z';

    const wrapper = mountModal(conflict);
    await flushPromises();

    expect(wrapper.text()).toContain('Overridden by:');
    expect(wrapper.text()).toContain('Taylor Ng');
    expect(wrapper.text()).toContain('August 11, 2026 at 10:30 AM');
    expect(wrapper.get('[data-test="ua-button"]').text()).toContain('Update');
  });

  it('displays Unknown user when an override has no audit user ID', () => {
    const conflict = createConflict(101, 102, 'First assignment', 'Second assignment');
    conflict.isOverridden = true;
    conflict.createdById = null;
    conflict.createdOn = '2026-08-11T16:00:00Z';

    const wrapper = mountModal(conflict);

    expect(wrapper.text()).toContain('Overridden by:');
    expect(wrapper.text()).toContain('Unknown user');
  });

  it('uses the comparison timezone and includes both dates for cross-midnight ranges', () => {
    const conflict = createConflict(101, 102, 'First assignment', 'Second assignment');
    conflict.entry.start = '2025-01-14T07:30:00Z';
    conflict.entry.end = '2025-01-14T08:30:00Z';
    conflict.entry.timeZoneId = 'America/Edmonton';
    conflict.overlapStart = conflict.entry.start;
    conflict.overlapEnd = conflict.entry.end;

    const wrapper = mountModal(conflict);

    expect(wrapper.text()).toContain('Times shown in America/Vancouver');
    expect(wrapper.text()).toContain('January 13, 2025, 11:30 PM - January 14, 2025, 12:30 AM');
    expect(wrapper.text()).toContain('Event timezone: America/Edmonton');
  });

  it('hides edit and override actions without permission', () => {
    const wrapper = mountModal(createConflict(101, 102, 'First assignment', 'Second assignment'), false);

    expect(wrapper.find('.calendar-conflict-detail__edit').exists()).toBe(false);
    expect(wrapper.find('[data-test="ua-button"]').exists()).toBe(false);
  });
});

function mountModal(conflict: CalendarConflict, canEdit = true) {
  return mount(CalendarConflictDetailModal, {
    props: {
      conflict,
      currentEventId: 101,
      timeZone: 'America/Vancouver',
      canEditEvent: canEdit,
      canOverride: canEdit,
    },
    global: {
      stubs: {
        UaModal: {
          props: ['title'],
          emits: ['close'],
          template: '<section><h2>{{ title }}</h2><slot /></section>',
        },
        UaAlert: {
          template: '<div><slot /></div>',
        },
        UaBtn: {
          props: ['color', 'disabled'],
          emits: ['click'],
          template:
            '<button data-test="ua-button" :data-color="color" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
        },
        UaTextarea: {
          props: ['modelValue', 'disabled'],
          emits: ['update:modelValue'],
          template:
            '<textarea :value="modelValue" :disabled="disabled" @input="$emit(\'update:modelValue\', $event.target.value)" />',
        },
        VIcon: {
          template: '<i />',
        },
      },
    },
  });
}

function createConflict(
  entryEventId: number,
  overlapsEventId: number,
  entryTitle: string,
  overlapsTitle: string,
): CalendarConflict {
  return {
    id: `conflict:${entryEventId}:${overlapsEventId}`,
    entry: {
      eventId: entryEventId,
      eventTypeCode: 'assignment',
      sourceModule: 'scheduling',
      title: entryTitle,
      start: '2025-01-13T10:00:00Z',
      end: '2025-01-13T12:00:00Z',
    },
    overlaps: {
      eventId: overlapsEventId,
      eventTypeCode: 'assignment',
      sourceModule: 'scheduling',
      title: overlapsTitle,
      start: '2025-01-13T11:00:00Z',
      end: '2025-01-13T13:00:00Z',
    },
    resourceId: 'user-1',
    overlapStart: '2025-01-13T11:00:00Z',
    overlapEnd: '2025-01-13T12:00:00Z',
    isOverridden: false,
  };
}
