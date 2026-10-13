import { computed, shallowRef } from 'vue';
import { getApiLeaveLeaveTypes } from '@/api-access/generated/leave-type/leave-type';
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

  const filterLeaveTypeOptionsByDate = (date: Date): SelectOption[] => {
    const dateOnly = date.toISOString().slice(0, 10);
    const filteredOptions = leaveTypeOptions.value.filter(({ effectiveDateUtc, expiryDateUtc }) => {
      const effectiveDate = effectiveDateUtc?.slice(0, 10);
      const expiryDate = expiryDateUtc?.slice(0, 10);

      return (!effectiveDate || effectiveDate <= dateOnly) && (!expiryDate || expiryDate > dateOnly);
    });
    return filteredOptions;
  };

  const activeLeaveTypeOptions = computed(() => filterLeaveTypeOptionsByDate(new Date()));

  const leaveTypesError = computed(() => (leaveTypesLoadError.value ? 'Unable to load leave types.' : ''));

  return {
    leaveTypeOptions,
    activeLeaveTypeOptions,
    filterLeaveTypeOptionsByDate,
    isLoadingLeaveTypes: computed(() => leaveTypes.value == null && leaveTypesRequest != null),
    leaveTypesError,
  };
}
