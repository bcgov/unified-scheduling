import { computed, ref, watch, type Ref } from 'vue';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type { SelectOption } from '@/types/select';
import { formatUserOptionLabel, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export function useSchedulingEmployeeOptions(
  locationId: Ref<number | null>,
  formData: Ref<ShiftResourceFormData>,
  options: { resource?: Ref<CalendarMatrixResource | undefined>; onError?: (message: string) => void } = {},
) {
  const isLoadingUsers = ref(false);
  const availableUsers = ref<UserResponse[]>([]);

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
    isLoadingUsers.value = true;

    try {
      const { data, error, execute } = getApiUsers(
        {
          IsEnabled: true,
          LocationId: nextLocationId ?? undefined,
        },
        {
          options: { immediate: false },
        },
      );

      await execute();

      if (error.value) {
        throw error.value;
      }

      availableUsers.value = data.value ?? [];
    } catch (error: unknown) {
      availableUsers.value = [];
      options.onError?.(error instanceof Error ? error.message : 'Failed to load employees.');
    } finally {
      isLoadingUsers.value = false;
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
