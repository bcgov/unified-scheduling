import { mount } from '@vue/test-utils';
import { defineComponent } from 'vue';
import { describe, expect, it } from 'vitest';

import CalendarSchedulingAssignmentEventContent from '@/modules/scheduling/CalendarSchedulingAssignmentEventContent.vue';
import type { CalendarSchedulingEvent } from '@/modules/scheduling/calendarSchedulingData';

const VMenuStub = defineComponent({
  template: '<div><slot name="activator" :props="{}" /><slot /></div>',
});

const VIconStub = defineComponent({
  template: '<span data-test="partial-warning-icon" />',
});

describe('CalendarSchedulingAssignmentEventContent', () => {
  it('renders assigned user names from event metadata when they are not in the visible user rows', () => {
    const event: CalendarSchedulingEvent = {
      id: 'assignment-1',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Yellow Assignment',
      start: '2026-07-13T15:30:00+00:00',
      end: '2026-07-14T00:00:00+00:00',
      metadata: {
        capacity: 1,
        assignedCount: 1,
        assignedUserIds: ['external-user'],
        assignedUsers: [{ id: 'external-user', type: 'user', title: 'Chief Sheriff' }],
      },
    };

    const wrapper = mount(CalendarSchedulingAssignmentEventContent, {
      props: {
        event,
        users: [],
      },
      global: {
        stubs: {
          VIcon: VIconStub,
          VMenu: VMenuStub,
        },
      },
    });

    expect(wrapper.text()).toContain('- C. Sheriff');
    expect(wrapper.text()).not.toContain('Unknown user');
    expect(wrapper.text()).not.toContain('external-user');
  });

  it('renders a partial coverage warning with user and shift time details', () => {
    const event: CalendarSchedulingEvent = {
      id: 'assignment-1',
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Yellow Assignment',
      start: '2026-07-13T15:30:00+00:00',
      end: '2026-07-14T00:00:00+00:00',
      timeZoneId: 'America/Vancouver',
      metadata: {
        capacity: 1,
        assignedCount: 1,
        assignedUserIds: ['user-1'],
        capacitySlotStates: ['partial'],
        partialCoverageShifts: [
          {
            userIds: ['user-1'],
            start: '2026-07-13T16:00:00+00:00',
            end: '2026-07-14T00:00:00+00:00',
            timeZoneId: 'America/Vancouver',
          },
        ],
      },
    };

    const wrapper = mount(CalendarSchedulingAssignmentEventContent, {
      props: {
        event,
        users: [{ id: 'user-1', type: 'user', title: 'Developer User' }],
      },
      global: {
        stubs: {
          VIcon: VIconStub,
          VMenu: VMenuStub,
        },
      },
    });

    expect(wrapper.find('[aria-label="Partial Coverage"]').exists()).toBe(true);
    expect(wrapper.find('[data-test="partial-warning-icon"]').exists()).toBe(true);
    expect(wrapper.text()).toContain('Partial coverage:');
    expect(wrapper.text()).toContain('D. User (');
    expect(wrapper.text()).toContain('9:00');
    expect(wrapper.text()).toContain('5:00');
  });
});
