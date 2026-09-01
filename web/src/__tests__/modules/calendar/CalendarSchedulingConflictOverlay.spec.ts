import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import CalendarSchedulingConflictOverlay from '@/modules/scheduling/CalendarSchedulingConflictOverlay.vue';
import type { CalendarConflict } from '@/modules/calendar/calendarTypes';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';

describe('CalendarSchedulingConflictOverlay', () => {
  it('lists every conflict with details of the conflicting event and emits resolution requests', async () => {
    const firstConflict = createConflict(101, 102, 'Second assignment', 'user-1');
    const duplicateResourceConflict = createConflict(101, 102, 'Second assignment', 'user-2');
    const thirdEventConflict = createConflict(101, 103, 'Third assignment', 'user-1');
    const event = createEvent();
    const wrapper = mount(CalendarSchedulingConflictOverlay, {
      props: {
        event,
        conflicts: [firstConflict, duplicateResourceConflict, thirdEventConflict],
        icon: 'warning-icon',
        timeZone: 'America/Vancouver',
      },
      global: {
        stubs: {
          VIcon: {
            props: ['icon'],
            template: '<i class="icon-stub">{{ icon }}</i>',
          },
        },
      },
    });

    expect(wrapper.get('.calendar-scheduling-conflict-overlay__heading').text()).toBe('Conflict(s)');
    expect(wrapper.text()).toContain('warning-icon');
    expect(wrapper.text()).toContain('Second assignment');
    expect(wrapper.text()).toContain('Third assignment');
    expect(wrapper.findAll('.calendar-scheduling-conflict-overlay__item')).toHaveLength(3);
    expect(wrapper.findAll('.calendar-scheduling-conflict-overlay__item')[0]?.text()).toContain('January 13, 2025');
    expect(wrapper.findAll('.calendar-scheduling-conflict-overlay__item')[0]?.text()).toContain('3:00 AM - 5:00 AM');

    const resolveButtons = wrapper.findAll('button').filter((button) => button.text().trim() === 'Resolve');
    await resolveButtons[0]?.trigger('click');

    expect(wrapper.emitted('resolve')).toEqual([[firstConflict]]);
  });

  it('uses the warning treatment only on overridden conflict actions', () => {
    const overriddenConflict = createConflict(101, 102, 'Overridden assignment', 'user-1');
    overriddenConflict.isOverridden = true;
    const unresolvedConflict = createConflict(101, 103, 'Unresolved assignment', 'user-1');
    const wrapper = mount(CalendarSchedulingConflictOverlay, {
      props: {
        event: createEvent(),
        conflicts: [overriddenConflict, unresolvedConflict],
        icon: 'warning-icon',
        timeZone: 'America/Vancouver',
      },
      global: {
        stubs: {
          VIcon: {
            props: ['icon'],
            template: '<i class="icon-stub">{{ icon }}</i>',
          },
        },
      },
    });

    const titles = wrapper.findAll('.calendar-scheduling-conflict-overlay__summary strong');
    const icons = wrapper.findAll('.calendar-scheduling-conflict-overlay__summary .icon-stub');
    const buttons = wrapper.findAll('.calendar-scheduling-conflict-overlay__resolve');
    expect(titles[0]?.classes()).toContain('calendar-scheduling-conflict-overlay__title--overridden');
    expect(titles[1]?.classes()).not.toContain('calendar-scheduling-conflict-overlay__title--overridden');
    expect(icons[0]?.classes()).toContain('calendar-scheduling-conflict-overlay__icon--overridden');
    expect(icons[1]?.classes()).not.toContain('calendar-scheduling-conflict-overlay__icon--overridden');
    expect(buttons[0]?.text()).toBe('View resolution');
    expect(buttons[0]?.classes()).toContain('calendar-scheduling-conflict-overlay__resolve--overridden');
    expect(buttons[1]?.text()).toBe('Resolve');
    expect(buttons[1]?.classes()).not.toContain('calendar-scheduling-conflict-overlay__resolve--overridden');
    expect(wrapper.get('.calendar-scheduling-conflict-overlay__heading').classes()).not.toContain(
      'calendar-scheduling-conflict-overlay__title--overridden',
    );
  });
});

function createEvent(): CalendarSchedulingEvent {
  return {
    id: 'assignment-entry-201',
    type: 'scheduling.assignment',
    sourceModule: 'scheduling',
    title: 'First assignment',
    start: '2025-01-13T10:00:00Z',
    end: '2025-01-13T12:00:00Z',
    metadata: {
      eventId: 101,
      assignmentEntryId: '201',
    },
  };
}

function createConflict(
  currentEventId: number,
  conflictingEventId: number,
  conflictingTitle: string,
  resourceId: string,
): CalendarConflict {
  return {
    id: `conflict:${currentEventId}:${conflictingEventId}:${resourceId}`,
    entry: {
      eventId: currentEventId,
      eventTypeCode: 'assignment',
      sourceModule: 'scheduling',
      title: 'First assignment',
      start: '2025-01-13T10:00:00Z',
      end: '2025-01-13T12:00:00Z',
    },
    overlaps: {
      eventId: conflictingEventId,
      eventTypeCode: 'assignment',
      sourceModule: 'scheduling',
      title: conflictingTitle,
      start: '2025-01-13T11:00:00Z',
      end: '2025-01-13T13:00:00Z',
    },
    resourceId,
    overlapStart: '2025-01-13T11:00:00Z',
    overlapEnd: '2025-01-13T12:00:00Z',
    isOverridden: false,
  };
}
