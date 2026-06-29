import type {
  StatCategoryResponse,
  StatGroupResponse,
  StatMetricResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsGroups } from '@/api-access/generated/stat-groups/stat-groups';
import { getApiStatsMetrics } from '@/api-access/generated/stat-metrics/stat-metrics';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiStatsSubCategoryMetrics } from '@/api-access/generated/sub-category-metrics/sub-category-metrics';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectValue } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { DAILY_REGULAR_TARGET_HOURS } from '../constants';
import type { DayAssignment } from '../types';
import { getMondayOfWeek, useWeeklyRecords } from './useWeeklyRecords';

export function useEnterHours(groupId: number) {
  const authStore = useAuthStore();
  const locationsStore = useLocationsStore();
  const { hasPermission } = useAccessControl();
  const route = useRoute();

  const canEnterForOthers = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));

  // ── Deep-link query params (optional pre-seed from dashboard edit) ─────────
  const seedUserId = route.query.userId as string | undefined;
  const parsedLocationId = Number(route.query.locationId);
  const seedLocationId = Number.isFinite(parsedLocationId) ? parsedLocationId : undefined;
  const rawDate = route.query.date as string | undefined;
  const seedDate = rawDate && DateTime.fromISO(rawDate).isValid ? rawDate : undefined;
  const seedEmployeeName = (route.query.employeeName as string | undefined) || undefined;

  // ── Reference data ────────────────────────────────────────────────────────
  const isLoadingReference = ref(true);
  const groups = ref<StatGroupResponse[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const metrics = ref<StatMetricResponse[]>([]);
  const subCategoryMetrics = ref<SubCategoryMetricResponse[]>([]);

  // ── Location ──────────────────────────────────────────────────────────────
  const locationOptions = computed(() => locationsStore.getSelectOptions());
  const selectedLocationId = ref<number | null>(seedLocationId ?? authStore.homeLocationId ?? null);

  const onLocationChange = (value: SelectValue | undefined) => {
    selectedLocationId.value = value != null ? Number(value) : null;
  };

  // ── User picker ───────────────────────────────────────────────────────────
  const locationUsers = ref<UserResponse[]>([]);
  const selectedUserId = ref<string | null>(seedUserId ?? (canEnterForOthers.value ? null : authStore.currentUserId));
  let activeLocationId: number | null = null;

  const userOptions = computed(() =>
    locationUsers.value.map((u) => ({ code: u.id, description: `${u.firstName} ${u.lastName}` })),
  );

  async function loadUsersForLocation(locationId: number) {
    activeLocationId = locationId;
    const { data } = await getApiUsers({ LocationId: locationId, IsEnabled: true });
    if (activeLocationId !== locationId) return;
    locationUsers.value = data.value ?? [];
    const currentUserId = authStore.currentUserId;
    selectedUserId.value =
      currentUserId != null && locationUsers.value.some((u) => u.id === currentUserId) ? currentUserId : null;
  }

  watch(selectedLocationId, async (locId) => {
    if (canEnterForOthers.value && locId) {
      locationUsers.value = [];
      selectedUserId.value = null;
      await loadUsersForLocation(locId);
    } else {
      selectedUserId.value = authStore.currentUserId;
    }
  });

  // ── Weekly records ────────────────────────────────────────────────────────
  const {
    weekDates,
    dayAssignmentsMap,
    dayStatusMap,
    daySummaryMap,
    weeklyRegularTotal,
    weeklyOvertimeTotal,
    isOvertimeEnabled,
    isLoading,
    error: loadError,
    loadWeek,
    saveDay,
    navigateWeek,
    createEmptyAssignment,
  } = useWeeklyRecords(
    getMondayOfWeek(seedDate ? DateTime.fromISO(seedDate) : DateTime.now()),
    selectedLocationId,
    selectedUserId,
    subCategories,
    categories,
    subCategoryMetrics,
    metrics,
  );

  // ── Selected day ──────────────────────────────────────────────────────────
  const selectedDate = ref<string | null>(null);
  const dayErrors = ref<Record<string, string>>({});
  const apiError = ref('');

  const selectedAssignments = computed({
    get: () => (selectedDate.value ? (dayAssignmentsMap.value[selectedDate.value] ?? []) : []),
    set: (val: DayAssignment[]) => {
      if (selectedDate.value) dayAssignmentsMap.value[selectedDate.value] = val;
    },
  });

  function onSelectDay(date: string) {
    selectedDate.value = date;
    if (!dayAssignmentsMap.value[date] || dayAssignmentsMap.value[date].length === 0) {
      dayAssignmentsMap.value[date] = [createEmptyAssignment(groupId)];
    }
    dayErrors.value = {};
    apiError.value = '';
  }

  function addAssignment() {
    if (!selectedDate.value) return;
    dayAssignmentsMap.value[selectedDate.value] = [...selectedAssignments.value, createEmptyAssignment(groupId)];
  }

  function removeAssignment(id: string) {
    if (!selectedDate.value) return;
    const remaining = selectedAssignments.value.filter((a) => a.id !== id);
    dayAssignmentsMap.value[selectedDate.value] = remaining.length > 0 ? remaining : [createEmptyAssignment(groupId)];
  }

  function updateAssignment(updated: DayAssignment) {
    if (!selectedDate.value) return;
    dayAssignmentsMap.value[selectedDate.value] = selectedAssignments.value.map((a) =>
      a.id === updated.id ? updated : a,
    );
  }

  // ── Copy from another day (within current week) ────────────────────────
  const copyFromOptions = computed(() =>
    weekDates.value
      .filter((d) => d !== selectedDate.value)
      .filter((d) => (dayAssignmentsMap.value[d] ?? []).some((a) => a.subCategoryId))
      .map((d) => {
        const dt = DateTime.fromISO(d);
        const count = (dayAssignmentsMap.value[d] ?? []).filter((a) => a.subCategoryId).length;
        return { date: d, label: `${dt.toFormat('EEE, MMM d')} (${count})` };
      }),
  );

  function copyFromDay(sourceDate: string) {
    if (!selectedDate.value) return;
    const source = (dayAssignmentsMap.value[sourceDate] ?? []).filter((a) => a.subCategoryId);
    if (source.length === 0) return;

    const cloned = source.map((a) => ({
      ...a,
      id: String(Date.now() + Math.random()),
      metricValues: { ...a.metricValues },
      existingRecordIds: {},
    }));

    const current = selectedAssignments.value;
    const hasOnlyEmpty = current.length === 1 && !current[0].subCategoryId;
    dayAssignmentsMap.value[selectedDate.value] = hasOnlyEmpty ? cloned : [...current, ...cloned];
  }

  // ── Validation ────────────────────────────────────────────────────────────
  function validate(assignments: DayAssignment[]): boolean {
    const errors: Record<string, string> = {};
    let dayTotalHours = 0;

    for (const [i, assignment] of assignments.entries()) {
      if (!assignment.categoryId) {
        errors[`assignment_${i}_category`] = 'Work Area is required';
      }
      if (!assignment.subCategoryId) {
        errors[`assignment_${i}_subCategory`] = 'Subcategory is required';
      }

      const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
      let hasValue = false;

      for (const scm of scms) {
        if (!scm.id) continue;
        const raw = assignment.metricValues[scm.id];
        if (!raw || raw.trim() === '') continue;

        const val = parseFloat(raw);
        if (isNaN(val)) {
          errors[`assignment_${i}_metric_${scm.id}`] = 'Must be a valid number';
          continue;
        }

        hasValue = true;
        const metric = metrics.value.find((m) => m.id === scm.metricId);
        const isRegular = metric?.unitOfMeasure === 'hours' && !metric.name?.toLowerCase().includes('overtime');

        if (isRegular && val > DAILY_REGULAR_TARGET_HOURS) {
          errors[`assignment_${i}_metric_${scm.id}`] =
            `Regular hours cannot exceed ${DAILY_REGULAR_TARGET_HOURS} per day`;
        }

        if (metric?.unitOfMeasure === 'hours') dayTotalHours += val;
      }

      if (!hasValue && assignment.subCategoryId) {
        errors[`assignment_${i}`] = 'Enter at least one metric value';
      }
    }

    if (dayTotalHours > 24) {
      errors['day'] = `Total hours (${dayTotalHours}h) cannot exceed 24h per day`;
    }

    dayErrors.value = errors;
    return Object.keys(errors).length === 0;
  }

  // ── Save ──────────────────────────────────────────────────────────────────
  const isSaving = ref(false);

  async function handleSave(status: string) {
    if (!selectedDate.value) return;
    if (!validate(selectedAssignments.value)) return;

    isSaving.value = true;
    apiError.value = '';
    try {
      const err = await saveDay(selectedDate.value, selectedAssignments.value, status, groupId);
      if (err) apiError.value = err;
    } finally {
      isSaving.value = false;
    }
  }

  // ── Mount ─────────────────────────────────────────────────────────────────
  onMounted(async () => {
    const [groupsRes, catsRes, subCatsRes, metricsRes, scmRes] = await Promise.all([
      getApiStatsGroups(),
      getApiStatsCategories(),
      getApiStatsSubCategories(),
      getApiStatsMetrics(),
      getApiStatsSubCategoryMetrics(),
    ]);
    groups.value = groupsRes.data.value ?? [];
    categories.value = catsRes.data.value ?? [];
    subCategories.value = subCatsRes.data.value ?? [];
    metrics.value = metricsRes.data.value ?? [];
    subCategoryMetrics.value = scmRes.data.value ?? [];

    const initialLocationId = seedLocationId ?? authStore.homeLocationId;
    if (initialLocationId) {
      const { data } = await getApiUsers({ LocationId: initialLocationId, IsEnabled: true });
      locationUsers.value = data.value ?? [];

      // If the seeded user isn't in this location's list, inject them so the dropdown shows their name.
      if (seedUserId && seedEmployeeName && !locationUsers.value.some((u) => u.id === seedUserId)) {
        const [firstName, ...rest] = seedEmployeeName.split(' ');
        locationUsers.value = [
          { id: seedUserId, firstName: firstName ?? '', lastName: rest.join(' ') } as UserResponse,
          ...locationUsers.value,
        ];
      }

      // Default the selected user to the current user if they belong to this location
      if (!seedUserId && !canEnterForOthers.value) {
        selectedUserId.value = authStore.currentUserId;
      }
    }

    isLoadingReference.value = false;

    if (seedDate && seedLocationId && seedUserId) {
      await loadWeek();
      onSelectDay(seedDate);
    } else if (initialLocationId && selectedUserId.value) {
      await loadWeek();
    }
  });

  const weekRangeLabel = computed(() => {
    const dates = weekDates.value;
    if (dates.length < 7) return '';
    const from = DateTime.fromISO(dates[0]);
    const to = DateTime.fromISO(dates[6]);
    return `${from.toFormat('MMM d')} – ${to.toFormat('MMM d')}, ${to.year}`;
  });

  return {
    canEnterForOthers,
    seedLocationId,
    seedUserId,
    isLoadingReference,
    groups,
    categories,
    subCategories,
    metrics,
    subCategoryMetrics,
    locationOptions,
    selectedLocationId,
    onLocationChange,
    selectedUserId,
    userOptions,
    weekDates,
    dayStatusMap,
    daySummaryMap,
    weeklyRegularTotal,
    weeklyOvertimeTotal,
    isOvertimeEnabled,
    isLoading,
    loadError,
    navigateWeek,
    weekRangeLabel,
    selectedDate,
    selectedAssignments,
    onSelectDay,
    addAssignment,
    removeAssignment,
    updateAssignment,
    copyFromOptions,
    copyFromDay,
    dayErrors,
    apiError,
    isSaving,
    handleSave,
  };
}
