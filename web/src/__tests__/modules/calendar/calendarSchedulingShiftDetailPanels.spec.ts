import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import CalendarSchedulingShiftDeletePanel from '@/modules/scheduling/CalendarSchedulingShiftDeletePanel.vue';
import CalendarSchedulingShiftDetailsPanel from '@/modules/scheduling/CalendarSchedulingShiftDetailsPanel.vue';
import CalendarSchedulingShiftEditPanel from '@/modules/scheduling/CalendarSchedulingShiftEditPanel.vue';
import CalendarSchedulingShiftForm from '@/modules/scheduling/CalendarSchedulingShiftForm.vue';
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
        locationOptions: [{ code: 1, description: 'HQ' }],
        employeeOptions: [{ code: 'user-1', description: 'Alex Alpha' }],
        showRecurrence: false,
      },
      global: {
        stubs: {
          CalendarSchedulingShiftForm: {
            props: ['modelValue', 'formErrors', 'locationOptions', 'employeeOptions', 'showRecurrence'],
            emits: ['recurrenceChange', 'recurrenceInvalid'],
            template:
              "<button class=\"form-stub\" @click=\"$emit('recurrenceChange', null); $emit('recurrenceInvalid', 'Invalid recurrence')\">{{ formErrors.date }} {{ locationOptions[0].description }} {{ employeeOptions[0].description }} {{ showRecurrence }}</button>",
          },
        },
      },
    });

    expect(wrapper.text()).toContain('Required');
    expect(wrapper.text()).toContain('HQ');
    expect(wrapper.text()).toContain('Alex Alpha');
    expect(wrapper.text()).toContain('false');

    await wrapper.get('.form-stub').trigger('click');

    expect(wrapper.emitted('recurrenceChange')?.[0]).toEqual([null]);
    expect(wrapper.emitted('recurrenceInvalid')?.[0]).toEqual(['Invalid recurrence']);
  });
});

describe('CalendarSchedulingShiftForm', () => {
  it('clears employees and linked assignments when location changes', async () => {
    const formData: ShiftResourceFormData = {
      locationId: 1,
      userIds: ['user-1'],
      assignmentEntryLinks: [{ assignmentEntryId: 42, assignedUserIds: ['user-1'] }],
      assignmentSeriesLinks: [{ assignmentSeriesId: 84, assignedUserIds: ['user-1'] }],
      date: '2026-07-03',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    };
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: formData,
        locationOptions: [
          { code: 1, description: 'HQ' },
          { code: 2, description: 'Branch' },
        ],
        employeeOptions: [{ code: 'user-1', description: 'Alex Alpha' }],
        showRecurrence: false,
      },
      global: {
        stubs: {
          UaSelect: {
            props: ['id', 'modelValue', 'items'],
            emits: ['update:modelValue'],
            template:
              '<button v-if="id === \'shift-form-location\'" class="location-select" @click="$emit(\'update:modelValue\', 2)">Location</button><div v-else />',
          },
        },
      },
    });

    await wrapper.get('.location-select').trigger('click');

    const emitted = wrapper.emitted('update:modelValue')?.[0]?.[0] as ShiftResourceFormData;
    expect(emitted).toMatchObject({
      locationId: 2,
      userIds: [],
      assignmentEntryLinks: [],
      assignmentSeriesLinks: [],
    });
  });

  it('removes a linked assignment entry with a single consistent model update', async () => {
    const formData: ShiftResourceFormData = {
      locationId: 1,
      userIds: ['user-1'],
      assignmentEntryLinks: [{ assignmentEntryId: 42, assignedUserIds: ['user-1'] }],
      date: '2026-07-31',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    };
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: formData,
        locationOptions: [{ code: 1, description: 'HQ' }],
        employeeOptions: [{ code: 'user-1', description: 'Alex Alpha' }],
        assignmentEntryOptions: [{ code: 42, description: 'Court coverage' }],
        showRecurrence: false,
      },
      global: {
        stubs: {
          UaBtn: {
            template: '<button v-bind="$attrs"><slot /></button>',
          },
        },
      },
    });

    await wrapper.get('button[aria-label="Remove Assignment 1"]').trigger('click');

    expect(wrapper.emitted('update:modelValue')).toHaveLength(1);
    const emitted = wrapper.emitted('update:modelValue')?.[0]?.[0] as ShiftResourceFormData;
    expect(emitted.assignmentEntryLinks).toEqual([]);
  });
});

describe('getShiftDeleteDisabledReason', () => {
  it('allows draft and published deletes and blocks cancelled shifts with scoped messages', () => {
    expect(getShiftDeleteDisabledReason('event', 'Draft')).toBe('');
    expect(getShiftDeleteDisabledReason('event', 'Active')).toBe('');
    expect(getShiftDeleteDisabledReason('event', 'Cancelled')).toBe(
      'Only draft or published shift entries can be deleted.',
    );
    expect(getShiftDeleteDisabledReason('series', 'Cancelled')).toBe(
      'Only draft or published shift series can be deleted.',
    );
  });
});
