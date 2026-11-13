import { computed, ref, watch, type Ref } from 'vue';
import type { LoanOutCalendarUser } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type { SelectOption } from '@/types/select';
import { formatUserOptionLabel, type ShiftResourceFormData } from './calendarSchedulingShiftForm';
import { useUsersStore } from '@/stores/Users';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useAccessControl } from '@/composables/useAccessControl';
import { useLoanOutSchedulingUsers } from '@/modules/loanout/api/loanOutApi';
import { defaultSchedulingTimeZoneId, resolveSchedulingTimeZoneId } from './schedulingTimeZone';
import { createLatestRequestGuard } from './latestRequestGuard';
import { isAvailableForSchedule } from './calendarSchedulingMappers';
import { DateTime } from 'luxon';

export function useSchedulingEmployeeOptions(
  locationId: Ref<number | null>,
  formData: Ref<ShiftResourceFormData>,
  options: { onError?: (message: string) => void } = {},
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

  function toUserSelectOptions(users: (UserResponse | LoanOutCalendarUser)[]): SelectOption[] {
    const usersById = new Map<string, UserResponse | LoanOutCalendarUser>();
    for (const user of users) {
      if (!usersById.has(user.id)) {
        usersById.set(user.id, user);
      }
    }
    return Array.from(usersById.values(), (user) => ({
      code: user.id,
      description: formatUserOptionLabel(user),
    }));
  }

  // Each dependency change creates a fresh cache shared by all three forms.
  const locationUsersByDate = computed(() => {
    const activeLocationId = locationId.value;
    const timeZone = resolveSchedulingTimeZoneId(
      activeLocationId ? locationsStore.entitiesMap[activeLocationId]?.timezone : undefined,
    );
    const candidates = [...availableUsers.value, ...usersStore.allUsers].map(
      (user) => usersStore.getUserById(user.id) ?? user,
    );
    const homeUsers = availableUsers.value;
    const cache = new Map<string, ReturnType<typeof getSchedulingLocationUsers>>();
    return (dateValue: string | undefined) => {
      if (!activeLocationId || !dateValue) return { homeUsers, formerHomeUsers: [] };
      let users = cache.get(dateValue);
      if (!users) {
        users = getSchedulingLocationUsers(
          DateTime.fromISO(dateValue, { zone: timeZone }).toJSDate(),
          candidates,
          activeLocationId,
          timeZone,
        );
        cache.set(dateValue, users);
      }
      return users;
    };
  });
  function getLocationUsersForDate(dateValue: string | undefined) {
    return locationUsersByDate.value(dateValue);
  }
  const locationUsersForDate = computed(() => getLocationUsersForDate(formData.value.date));

  function getHomeEmployeeOptions(date: string | undefined): SelectOption[] {
    const { homeUsers, formerHomeUsers } = getLocationUsersForDate(date);
    return toUserSelectOptions([...homeUsers, ...formerHomeUsers]);
  }

  // Current home members, employees loaned in for the date, and historical location members.
  const employeeOptions = computed<SelectOption[]>(() =>
    toUserSelectOptions([
      ...locationUsersForDate.value.homeUsers,
      ...availableLoanOutUsers.value,
      ...locationUsersForDate.value.formerHomeUsers,
    ]),
  );
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

    try {
      const [locationResult, allUsersResult] = await Promise.allSettled([
        usersStore.ensureUsersForLocation(nextLocationId),
        usersStore.ensureAllUsers(),
      ]);
      if (!requestGuard.isCurrent(requestId)) {
        return;
      }
      availableUsers.value = locationResult.status === 'fulfilled' ? locationResult.value : [];
      for (const result of [locationResult, allUsersResult]) {
        if (result.status === 'rejected') {
          options.onError?.(result.reason instanceof Error ? result.reason.message : 'Failed to load employees.');
        }
      }
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
    getHomeEmployeeOptions,
    isLoadingUsers,
    loadEmployeeOptions,
  };
}

export function getSchedulingLocationUsers(
  date: Date,
  candidates: Iterable<UserResponse | string>,
  locationId: number | undefined,
  timeZone = defaultSchedulingTimeZoneId,
  homeLocationUserIds: ReadonlySet<string> = new Set(),
) {
  const usersStore = useUsersStore();
  const homeUsers: UserResponse[] = [];
  const formerHomeUsers: UserResponse[] = [];
  const seen = new Set<string>();
  for (const candidate of candidates) {
    const userId = typeof candidate === 'string' ? candidate : candidate.id;
    if (seen.has(userId)) continue;
    seen.add(userId);
    const user = usersStore.getUserById(userId) ?? (typeof candidate === 'string' ? undefined : candidate);
    if (!user || !isAvailableForSchedule(date, user, locationId, timeZone, homeLocationUserIds)) continue;
    (user.homeLocationId === locationId ? homeUsers : formerHomeUsers).push(user);
  }
  return { homeUsers, formerHomeUsers };
}
