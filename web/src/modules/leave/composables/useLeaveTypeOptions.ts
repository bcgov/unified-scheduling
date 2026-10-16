import { computed, shallowRef } from 'vue';
import { DateTime } from 'luxon';
import { getApiLeaveLeaveTypes } from '@/api-access/generated/leave-type/leave-type';
import { resolveSchedulingTimeZoneId } from '@/modules/scheduling/schedulingTimeZone';
import { isUtcBusinessDateRangeActiveOn } from '@/utils/utcBusinessDate';
import type { SelectOption } from '@/types/select';

const leaveTypes = shallowRef<Awaited<ReturnType<typeof getApiLeaveLeaveTypes>>['data']['value']>(null);
const leaveTypesLoadError = shallowRef<Error | null>(null);
let leaveTypesRequest: Promise<void> | null = null;

function loadLeaveTypes() {
  if ((leaveTypes.value?.length ?? 0) > 0 || leaveTypesRequest) return;

  leaveTypesRequest = (async () => {
    try {
      const result = getApiLeaveLeaveTypes({ options: { immediate: false } });
      await result.execute();
      if (result.error.value) throw result.error.value;
      leaveTypes.value = result.data.value ?? [];
      leaveTypesLoadError.value = null;
    } catch (error: unknown) {
      leaveTypesLoadError.value = error instanceof Error ? error : new Error('Unable to load leave types.');
    } finally {
      leaveTypesRequest = null;
    }
  })();
}

export function useLeaveTypeOptions() {
  loadLeaveTypes();

  const leaveTypeOptions = computed(() =>
    (leaveTypes.value ?? []).flatMap((leaveType) =>
      leaveType.id == null
        ? []
        : [
            {
              code: leaveType.id,
              description: leaveType.name?.trim() || leaveType.description?.trim() || `Leave type ${leaveType.id}`,
              effectiveDateUtc: leaveType.effectiveDateUtc,
              expiryDateUtc: leaveType.expiryDateUtc,
            },
          ],
    ),
  );

  // Effective/expiry are UTC business dates; the leave date is the calendar day in the scheduling time zone.
  // Effective is inclusive, expiry is exclusive.
  const filterLeaveTypeOptionsByDate = (date: DateTime, timeZoneId?: string | null): SelectOption[] => {
    if (!date.isValid) return leaveTypeOptions.value;

    const targetDate = date.setZone(resolveSchedulingTimeZoneId(timeZoneId)).toISODate();
    if (!targetDate) return leaveTypeOptions.value;

    return leaveTypeOptions.value.filter(({ effectiveDateUtc, expiryDateUtc }) =>
      isUtcBusinessDateRangeActiveOn(effectiveDateUtc, expiryDateUtc, targetDate),
    );
  };

  const activeLeaveTypeOptions = computed(() => filterLeaveTypeOptionsByDate(DateTime.now()));

  const leaveTypesError = computed(() => (leaveTypesLoadError.value ? 'Unable to load leave types.' : ''));

  return {
    leaveTypeOptions,
    activeLeaveTypeOptions,
    filterLeaveTypeOptionsByDate,
    isLoadingLeaveTypes: computed(() => leaveTypes.value == null && leaveTypesRequest != null),
    leaveTypesError,
  };
}
