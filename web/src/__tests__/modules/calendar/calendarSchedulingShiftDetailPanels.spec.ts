import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import CalendarSchedulingShiftDeletePanel from '@/modules/scheduling/CalendarSchedulingShiftDeletePanel.vue';
import CalendarSchedulingShiftDetailsPanel from '@/modules/scheduling/CalendarSchedulingShiftDetailsPanel.vue';
import CalendarSchedulingShiftEditPanel from '@/modules/scheduling/CalendarSchedulingShiftEditPanel.vue';
import CalendarSchedulingShiftForm from '@/modules/scheduling/CalendarSchedulingShiftForm.vue';
import CalendarSchedulingWorkingHoursModal from '@/modules/scheduling/CalendarSchedulingWorkingHoursModal.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { getShiftDeleteDisabledReason } from '@/modules/scheduling/useSchedulingShiftDelete';
import type { ShiftResourceFormData } from '@/modules/scheduling/calendarSchedulingShiftForm';
import type { SelectOption } from '@/types/select';

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
  it('hides publish and clears stale publish state without permission', async () => {
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: {
          locationId: 1,
          date: '2026-07-03',
          repeatMode: 'never',
          publish: 'yes',
          cancel: 'no',
          statusTypeCode: 'Draft',
        },
        locationOptions: [{ code: 1, description: 'HQ' }],
        employeeOptions: [],
        canPublish: false,
      },
    });

    expect(wrapper.find('[aria-label="Publish"]').exists()).toBe(false);
    const emitted = wrapper.emitted('update:modelValue')?.[0]?.[0] as ShiftResourceFormData;
    expect(emitted.publish).toBe('no');
  });

  it('offers lunch increments and excludes worked lunch above the available amount', () => {
    const formData: ShiftResourceFormData = {
      lunchAvailableMinutes: 30,
      workedLunchMinutes: 15,
      date: '2026-07-03',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    };
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: formData,
        locationOptions: [],
        employeeOptions: [],
        showRecurrence: false,
      },
      global: {
        stubs: {
          UaSelect: {
            props: ['id', 'items'],
            template: '<div :id="id" :data-options="items.map((item) => item.description).join(\',\')" />',
          },
        },
      },
    });

    expect(wrapper.get('#shift-form-lunch-available').attributes('data-options')).toBe('15 min,30 min,45 min,1 hour');
    expect(wrapper.get('#shift-form-worked-lunch').attributes('data-options')).toBe('0 min,15 min,30 min');
  });

  it('reduces worked lunch when lunch available is reduced', async () => {
    const formData: ShiftResourceFormData = {
      lunchAvailableMinutes: 60,
      workedLunchMinutes: 45,
      date: '2026-07-03',
      repeatMode: 'never',
      publish: 'no',
      cancel: 'no',
    };
    const wrapper = mount(CalendarSchedulingShiftForm, {
      props: {
        modelValue: formData,
        locationOptions: [],
        employeeOptions: [],
        showRecurrence: false,
      },
      global: {
        stubs: {
          UaSelect: {
            props: ['id'],
            emits: ['update:modelValue'],
            template:
              '<button v-if="id === \'shift-form-lunch-available\'" class="lunch-available" @click="$emit(\'update:modelValue\', 30)">Lunch</button><div v-else />',
          },
        },
      },
    });

    await wrapper.get('.lunch-available').trigger('click');

    const emitted = wrapper.emitted('update:modelValue')?.[0]?.[0] as ShiftResourceFormData;
    expect(emitted).toMatchObject({ lunchAvailableMinutes: 30, workedLunchMinutes: 30 });
  });

  it('clears employees and linked assignments when location changes', async () => {
    const formData: ShiftResourceFormData = {
      locationId: 1,
      userIds: ['user-1'],
      assignmentEntryId: 42,
      assignmentEntryIds: [42],
      assignmentEntryLinks: [{ assignmentEntryId: 42, assignedUserIds: ['user-1'] }],
      assignmentSeriesId: 84,
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
      assignmentEntryId: null,
      assignmentEntryIds: [],
      assignmentEntryLinks: [],
      assignmentSeriesId: null,
      assignmentSeriesLinks: [],
    });
  });

  it('removes a linked assignment entry with a single consistent model update', async () => {
    const formData: ShiftResourceFormData = {
      locationId: 1,
      userIds: ['user-1'],
      assignmentEntryId: 42,
      assignmentEntryIds: [42],
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
    expect(emitted.assignmentEntryIds).toEqual([]);
  });

  it('keeps linked-assignment user options stable when the selection changes', async () => {
    const formData: ShiftResourceFormData = {
      locationId: 1,
      userIds: ['user-1', 'user-2'],
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
        employeeOptions: [
          { code: 'user-1', description: 'Alex Alpha' },
          { code: 'user-2', description: 'Blair Beta' },
        ],
        showRecurrence: false,
      },
    });
    const vm = wrapper.vm as unknown as { currentShiftUserOptions: SelectOption[] };
    const initialUserOptions = vm.currentShiftUserOptions;

    await wrapper.setProps({
      modelValue: {
        ...formData,
        assignmentEntryLinks: [{ assignmentEntryId: 42, assignedUserIds: ['user-1', 'user-2'] }],
      },
    });

    expect(vm.currentShiftUserOptions).toBe(initialUserOptions);
  });
});

describe('CalendarSchedulingWorkingHoursModal', () => {
  it('displays the backend-provided working-hours breakdown', async () => {
    const app = await createTestApp({ loadConfig: false });
    const wrapper = mount(CalendarSchedulingWorkingHoursModal, {
      props: {
        userName: 'Alex Alpha',
        result: {
          userId: '00000000-0000-0000-0000-000000000001',
          date: '2026-09-08',
          paidShiftMinutes: 480,
          lunchAvailableMinutes: 60,
          workedLunchMinutes: 30,
          paidOutsideShiftMinutes: 0,
          creditedMinutes: 510,
          overtimeMinutes: 30,
          shiftEntryId: 42,
        },
      },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    expect(wrapper.getComponent(UaModal).props('title')).toBe('Alex Alpha · September 8, 2026');
    expect(document.body.textContent).toContain('Lunch break');
    expect(document.body.textContent).toContain('1h');
    expect(document.body.textContent).toContain('Worked lunch (included in paid shift time)');
    expect(document.body.textContent).toContain('30m');
    expect(document.body.textContent).toContain('Paid shift time');
    expect(document.body.textContent).toContain('8h');
    expect(document.body.textContent).toContain('Paid outside-shift assignments');
    expect(document.body.textContent).toContain('Regular credited time');
    expect(document.body.textContent).toContain('Total credited time');
    expect(document.body.textContent).toContain('8h 30m');
    expect(document.body.textContent).toContain('Overtime');

    wrapper.getComponent(UaModal).vm.$emit('close');
    expect(wrapper.emitted('close')).toHaveLength(1);

    wrapper.unmount();
  });
});

describe('getShiftDeleteDisabledReason', () => {
  it('allows draft deletion and published cancellation while blocking other lifecycle states', () => {
    expect(getShiftDeleteDisabledReason('event', 'Draft')).toBe('');
    expect(getShiftDeleteDisabledReason('event', 'Active')).toBe('');
    expect(getShiftDeleteDisabledReason('series', 'Active')).toBe('');
    expect(getShiftDeleteDisabledReason('event', 'Expired')).toBe(
      'Only draft or published shift entries can be deleted.',
    );
    expect(getShiftDeleteDisabledReason('series', 'Expired')).toBe(
      'Only draft or published shift series can be deleted.',
    );
  });
});
