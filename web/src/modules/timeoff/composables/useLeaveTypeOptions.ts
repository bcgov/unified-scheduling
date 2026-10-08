import { computed } from 'vue';
import { getApiLeaveLeaveTypes } from '@/api-access/generated/leave-type/leave-type';
import type { LeaveTypeResponse } from '@/api-access/generated/models';
import type { SelectOption } from '@/types/select';

const isActive = (leaveType: LeaveTypeResponse, now: Date) =>
  !leaveType.expiryDateUtc || new Date(leaveType.expiryDateUtc) > now;

export function useLeaveTypeOptions() {
  const { data, error, isFetching } = getApiLeaveLeaveTypes();

  const leaveTypeOptions = computed<SelectOption[]>(() => {
    const now = new Date();
    return (data.value ?? [])
      .filter((leaveType) => leaveType.id != null && isActive(leaveType, now))
      .map((leaveType) => ({
        code: leaveType.id as number,
        description: leaveType.name?.trim() || leaveType.description?.trim() || `Leave type ${leaveType.id}`,
      }));
  });

  const leaveTypesError = computed(() => (error.value ? 'Unable to load leave types.' : ''));

  return { leaveTypeOptions, isLoadingLeaveTypes: isFetching, leaveTypesError };
}
