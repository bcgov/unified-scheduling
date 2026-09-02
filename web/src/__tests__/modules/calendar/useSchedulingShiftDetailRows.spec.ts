import { computed, ref } from 'vue';
import { describe, expect, it } from 'vitest';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { createInitialShiftFormDataForCreateAction } from '@/modules/scheduling/calendarSchedulingShiftForm';
import { useSchedulingShiftDetailRows } from '@/modules/scheduling/useSchedulingShiftDetailRows';

describe('useSchedulingShiftDetailRows', () => {
  it('displays the fields supported by the shift edit form', () => {
    const event = computed<CalendarEventBase>(() => ({
      id: 'scheduling.shift-entry.200',
      type: 'scheduling.shift',
      sourceModule: 'scheduling',
      title: 'Developer User shift',
      start: '2026-07-13T16:00:00Z',
      end: '2026-07-14T00:00:00Z',
      timeZoneId: 'America/Vancouver',
      allDay: false,
      resourceIds: ['user-1'],
      statusTypeCode: 'draft',
    }));
    const formData = ref({
      ...createInitialShiftFormDataForCreateAction(1),
      date: '2026-07-13',
      startTime: '09:00',
      endTime: '17:00',
      statusTypeCode: 'Draft',
      publish: 'yes' as const,
      userIds: ['user-1'],
      assignmentEntryLinks: [{ assignmentEntryId: 42, assignedUserIds: ['user-1'] }],
      trainingLabel: '',
      notes: 'Bring laptop',
    });

    const { detailRows } = useSchedulingShiftDetailRows({
      event,
      selectedOpenScope: ref('event'),
      selectedSeries: ref(null),
      formData,
      employeeOptions: computed(() => [{ code: 'user-1', description: 'Developer User' }]),
      assignmentEntryOptions: computed(() => [{ code: 42, description: 'Yellow Assignment (9:00 AM - 5:00 PM)' }]),
      assignmentSeriesOptions: computed(() => []),
      locationOptions: computed(() => [{ code: 1, description: 'HQ' }]),
    });

    expect(detailRows.value.map((row) => row.label)).toEqual([
      'Location',
      'Employee',
      'Date',
      'Time',
      'Assignment(s)',
      'Training',
      'Publish',
      'Notes',
    ]);
    expect(detailRows.value.find((row) => row.label === 'Location')?.value).toBe('HQ');
    expect(detailRows.value.find((row) => row.label === 'Employee')?.value).toBe('Developer User');
    expect(detailRows.value.find((row) => row.label === 'Date')?.value).toBe('July 13, 2026');
    expect(detailRows.value.find((row) => row.label === 'Time')?.value).toBe('9:00 AM - 5:00 PM');
    expect(detailRows.value.find((row) => row.label === 'Assignment(s)')?.value).toBe(
      'Yellow Assignment (9:00 AM - 5:00 PM) — Users: Developer User',
    );
    expect(detailRows.value.find((row) => row.label === 'Publish')?.value).toBe('Yes');
  });
});
