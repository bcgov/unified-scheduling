import { computed, ref, watch, type Ref } from 'vue';
import type { LoanOutCalendarUser } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type { SelectOption } from '@/types/select';
import { formatUserOptionLabel, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { useUsersStore } from '@/stores/Users';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLoanOutSchedulingUsers } from '@/modules/loanout/api/loanOutApi';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import { createLatestRequestGuard } from './latestRequestGuard';

export function useSchedulingEmployeeOptions(
  locationId: Ref<number | null>,
  formData: Ref<ShiftResourceFormData>,
  options: { resource?: Ref<CalendarMatrixResource | undefined>; onError?: (message: string) => void } = {},
) {
  const usersStore = useUsersStore();
  const locationsStore = useLocationsStore();
  const accessControl = useAccessControl();
  const canLoadLoanOutUsers = computed(
    () =>
      (accessControl.featureFlags?.value?.LoanOut?.enabled ?? false) &&
      accessControl.hasPermission(Permissions.LoanOutView),
  );
  const isLoadingUsers = ref(false);
  const availableUsers = ref<UserResponse[]>([]);
  const availableLoanOutUsers = ref<LoanOutCalendarUser[]>([]);
  const requestGuard = createLatestRequestGuard();

  const employeeOptions = computed<SelectOption[]>(() => {
    const usersById = new Map<string, UserResponse | LoanOutCalendarUser>();
    for (const user of [...availableUsers.value, ...availableLoanOutUsers.value]) {
      if (!usersById.has(user.id)) {
        usersById.set(user.id, user);
      }
    }
    return Array.from(usersById.values(), (user) => ({
      code: user.id,
      description: formatUserOptionLabel(user),
    }));
  });

  async function loadEmployeeOptions(nextLocationId: number | null) {
    const requestId = requestGuard.begin();
    const date = formData.value.date;
    availableLoanOutUsers.value = [];
    if (!nextLocationId) {
      availableUsers.value = [];
      isLoadingUsers.value = false;
      return;
    }

    isLoadingUsers.value = true;

    let loadingLocationUsers = true;
    try {
      const users = await usersStore.ensureUsersForLocation(nextLocationId);
      if (!requestGuard.isCurrent(requestId)) {
        return;
      }
      availableUsers.value = users;
      loadingLocationUsers = false;
      if (!date || !canLoadLoanOutUsers.value) {
        return;
      }
      const timeZoneId = resolveSchedulingTimeZoneId(locationsStore.entitiesMap[nextLocationId]?.timezone);
      const result = useLoanOutSchedulingUsers({
        startDate: date,
        endDate: date,
        locationId: nextLocationId,
        timeZoneId,
      });
      await result.execute();
      if (result.error.value) {
        throw result.error.value;
      }
      if (!result.data.value) {
        throw new Error('Failed to load loan-out employees.');
      }
      if (requestGuard.isCurrent(requestId)) {
        availableLoanOutUsers.value = result.data.value;
      }
    } catch (error: unknown) {
      if (requestGuard.isCurrent(requestId)) {
        if (loadingLocationUsers) {
          availableUsers.value = [];
        }
        availableLoanOutUsers.value = [];
        options.onError?.(error instanceof Error ? error.message : 'Failed to load employees.');
      }
    } finally {
      if (requestGuard.isCurrent(requestId)) {
        isLoadingUsers.value = false;
      }
    }
  }

  watch(
    [locationId, () => formData.value.date, canLoadLoanOutUsers],
    ([nextLocationId]) => loadEmployeeOptions(nextLocationId),
    { immediate: true },
  );

  return {
    availableUsers,
    employeeOptions,
    isLoadingUsers,
    loadEmployeeOptions,
  };
}
