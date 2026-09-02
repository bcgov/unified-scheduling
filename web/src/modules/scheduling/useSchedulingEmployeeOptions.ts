import { computed, ref, watch, type Ref } from 'vue';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type { SelectOption } from '@/types/select';
import { formatUserOptionLabel, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { useSchedulingUsersStore } from './useSchedulingUsersStore';
import { createLatestRequestGuard } from './latestRequestGuard';

export function useSchedulingEmployeeOptions(
  locationId: Ref<number | null>,
  formData: Ref<ShiftResourceFormData>,
  options: { resource?: Ref<CalendarMatrixResource | undefined>; onError?: (message: string) => void } = {},
) {
  const schedulingUsersStore = useSchedulingUsersStore();
  const isLoadingUsers = ref(false);
  const availableUsers = ref<UserResponse[]>([]);
  const requestGuard = createLatestRequestGuard();

  const employeeOptions = computed<SelectOption[]>(() => {
    const selectOptions = availableUsers.value.map((user) => ({
      code: user.id,
      description: formatUserOptionLabel(user),
    }));

    const resource = options.resource?.value;
    if (resource?.type === 'user' && !selectOptions.some((option) => option.code === resource.id)) {
      selectOptions.unshift({
        code: resource.id,
        description: resource.title || resource.id,
      });
    }

    for (const userId of formData.value.userIds ?? []) {
      if (!selectOptions.some((option) => option.code === userId)) {
        selectOptions.unshift({ code: userId, description: userId });
      }
    }

    return selectOptions;
  });

  async function loadEmployeeOptions(nextLocationId: number | null) {
    const requestId = requestGuard.begin();
    if (!nextLocationId) {
      availableUsers.value = [];
      isLoadingUsers.value = false;
      return;
    }

    isLoadingUsers.value = true;

    try {
      const users = await schedulingUsersStore.ensureUsersForLocation(nextLocationId);
      if (requestGuard.isCurrent(requestId)) {
        availableUsers.value = users;
      }
    } catch (error: unknown) {
      if (requestGuard.isCurrent(requestId)) {
        availableUsers.value = [];
        options.onError?.(error instanceof Error ? error.message : 'Failed to load employees.');
      }
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        isLoadingUsers.value = false;
      }
    }
  }

  watch(locationId, loadEmployeeOptions, { immediate: true });

  return {
    availableUsers,
    employeeOptions,
    isLoadingUsers,
    loadEmployeeOptions,
  };
}
