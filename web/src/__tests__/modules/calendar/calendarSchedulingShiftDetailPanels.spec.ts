import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import CalendarSchedulingShiftDeletePanel from '@/modules/scheduling/CalendarSchedulingShiftDeletePanel.vue';
import CalendarSchedulingShiftDetailsPanel from '@/modules/scheduling/CalendarSchedulingShiftDetailsPanel.vue';
import CalendarSchedulingShiftEditPanel from '@/modules/scheduling/CalendarSchedulingShiftEditPanel.vue';
import { getShiftDeleteDisabledReason } from '@/modules/scheduling/useSchedulingShiftDelete';
import type { ShiftResourceFormData } from '@/modules/scheduling/calendarSchedulingShiftForm';

const detailRows = [
  { label: 'Assignee(s)', value: 'Alex Alpha' },
  { label: 'Date', value: 'July 3, 2026' },
  { label: 'Time', value: '9:00 AM - 5:00 PM' },
  { label: 'Notes', value: 'Test notes' },
  {
    label: 'Repeat',
    value: '',
    recurrenceRule: 'RRULE:FREQ=WEEKLY;COUNT=1',
    recurrenceStartDate: '2026-07-03T16:00:00Z',
  },
];

describe('CalendarSchedulingShiftDetailsPanel', () => {
  it('renders detail rows and the read-only recurrence display', () => {
    const wrapper = mount(CalendarSchedulingShiftDetailsPanel, {
      props: { detailRows },
      global: {
        stubs: {
          RRuleEditor: {
            props: ['modelValue', 'readOnly'],
            template: '<span class="rrule-stub">{{ modelValue }} {{ readOnly }}</span>',
          },
        },
      },
    });

    expect(wrapper.text()).toContain('Assignee(s)');
    expect(wrapper.text()).toContain('Alex Alpha');
    expect(wrapper.text()).toContain('Repeat');
    expect(wrapper.find('.rrule-stub').text()).toContain('RRULE:FREQ=WEEKLY;COUNT=1');
  });
});

describe('CalendarSchedulingShiftDeletePanel', () => {
  it('shows the disabled reason instead of confirmation controls when delete is blocked', () => {
    const wrapper = mount(CalendarSchedulingShiftDeletePanel, {
      props: {
        detailRows,
        deleteDisabledReason: 'Only draft shift entries can be deleted.',
        isDeleteConfirmed: false,
      },
      global: {
        stubs: {
          CalendarSchedulingShiftDetailsPanel: {
            template: '<div class="details-stub" />',
          },
          'v-checkbox': {
            template: '<input class="checkbox-stub" />',
          },
        },
      },
    });

    expect(wrapper.text()).toContain('Only draft shift entries can be deleted.');
    expect(wrapper.find('.checkbox-stub').exists()).toBe(false);
  });

  it('emits confirmation changes when delete is allowed', async () => {
    const wrapper = mount(CalendarSchedulingShiftDeletePanel, {
      props: {
        detailRows,
        deleteDisabledReason: '',
        isDeleteConfirmed: false,
      },
      global: {
        stubs: {
          CalendarSchedulingShiftDetailsPanel: {
            template: '<div class="details-stub" />',
          },
          'v-checkbox': {
            emits: ['update:modelValue'],
            template: '<button class="checkbox-stub" @click="$emit(\'update:modelValue\', true)">Confirm</button>',
          },
        },
      },
    });

    await wrapper.get('.checkbox-stub').trigger('click');

    expect(wrapper.emitted('update:isDeleteConfirmed')?.[0]).toEqual([true]);
  });
});

describe('CalendarSchedulingShiftEditPanel', () => {
  it('passes form props through and forwards recurrence events', async () => {
    const formData: ShiftResourceFormData = {
      date: '2026-07-03',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    };
    const wrapper = mount(CalendarSchedulingShiftEditPanel, {
      props: {
        modelValue: formData,
        formErrors: { date: 'Required' },
        employeeOptions: [{ code: 'user-1', description: 'Alex Alpha' }],
        showRecurrence: false,
      },
      global: {
        stubs: {
          CalendarSchedulingShiftForm: {
            props: ['modelValue', 'formErrors', 'employeeOptions', 'showRecurrence'],
            emits: ['recurrenceChange', 'recurrenceInvalid'],
            template:
              "<button class=\"form-stub\" @click=\"$emit('recurrenceChange', null); $emit('recurrenceInvalid', 'Invalid recurrence')\">{{ formErrors.date }} {{ employeeOptions[0].description }} {{ showRecurrence }}</button>",
          },
        },
      },
    });

    expect(wrapper.text()).toContain('Required');
    expect(wrapper.text()).toContain('Alex Alpha');
    expect(wrapper.text()).toContain('false');

    await wrapper.get('.form-stub').trigger('click');

    expect(wrapper.emitted('recurrenceChange')?.[0]).toEqual([null]);
    expect(wrapper.emitted('recurrenceInvalid')?.[0]).toEqual(['Invalid recurrence']);
  });
});

describe('getShiftDeleteDisabledReason', () => {
  it('allows draft deletes and blocks active event or series deletes with scoped messages', () => {
    expect(getShiftDeleteDisabledReason('event', 'Draft')).toBe('');
    expect(getShiftDeleteDisabledReason('event', 'Active')).toBe('Only draft shift entries can be deleted.');
    expect(getShiftDeleteDisabledReason('series', 'Active')).toBe('Only draft shift series can be deleted.');
  });
});
