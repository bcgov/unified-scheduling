import { computed, ref } from 'vue';
import { describe, expect, it } from 'vitest';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { createInitialShiftFormDataForCreateAction } from '@/modules/scheduling/calendarSchedulingShiftForm';
import { useSchedulingShiftMutation } from '@/modules/scheduling/useSchedulingShiftMutation';

describe('useSchedulingShiftMutation', () => {
  it('reports a missing active location as a context error', async () => {
    const mutation = useSchedulingShiftMutation({
      event: computed<CalendarEventBase>(() => ({
        id: 'scheduling.shift-entry.42',
        type: 'scheduling.shift',
        sourceModule: 'scheduling',
        title: 'Registry shift',
        start: '2026-07-13T16:00:00Z',
        end: '2026-07-14T00:00:00Z',
      })),
      formData: ref({
        ...createInitialShiftFormDataForCreateAction(null),
        date: '2026-07-13',
        startTime: '09:00',
        endTime: '17:00',
      }),
      selectedOpenScope: ref('event'),
      activeTimeZoneId: computed(() => 'America/Vancouver'),
      activeLocationId: computed(() => null),
      existingRecurrenceRule: computed(() => null),
    });

    await expect(mutation.saveShift()).resolves.toBe(false);
    expect(mutation.apiError.value).toBe('A location is required before saving this shift.');
    expect(mutation.formErrors.value).toEqual({});
    expect(mutation.isSaving.value).toBe(false);
  });
});
