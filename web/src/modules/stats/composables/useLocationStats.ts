import type {
  StatCategoryResponse,
  StatGroupResponse,
  StatMetricResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsGroups } from '@/api-access/generated/stat-groups/stat-groups';
import { getApiStatsMetrics } from '@/api-access/generated/stat-metrics/stat-metrics';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiStatsSubCategoryMetrics } from '@/api-access/generated/sub-category-metrics/sub-category-metrics';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import type { SelectValue } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { LOCATION_LEVEL_GROUP_ID } from '../constants';
import type { DayAssignment } from '../types';
import { getSundayOfWeek, useWeeklyRecords } from './useWeeklyRecords';

/**
 * Composable for the Location Level stats form (GroupId 3).
 * Simplified version of useEnterHours — no employee picker, no overtime logic.
 */
export function useLocationStats() {
  const groupId = LOCATION_LEVEL_GROUP_ID;
  const route = useRoute();
  const authStore = useAuthStore();
  const locationsStore = useLocationsStore();

  // ── Deep-link query params (from search/dashboard edit) ──────────────────
  const parsedLocationId = Number(route.query.locationId);
  const seedLocationId = Number.isFinite(parsedLocationId) ? parsedLocationId : undefined;
  const rawDate = route.query.date as string | undefined;
  const seedDate = rawDate && DateTime.fromISO(rawDate).isValid ? rawDate : undefined;

  const accountWarning = computed(() => {
    if (!authStore.currentUserId) {
      return 'Your account is not set up for data entry. Please contact your administrator.';
    }
    return null;
  });

  // ── Reference data ────────────────────────────────────────────────────────
  const isLoadingReference = ref(true);
  const groups = ref<StatGroupResponse[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const metrics = ref<StatMetricResponse[]>([]);
  const subCategoryMetrics = ref<SubCategoryMetricResponse[]>([]);

  // ── Location (no employee picker) ─────────────────────────────────────────
  const locationOptions = computed(() => locationsStore.selectOptions);
  const selectedLocationId = ref<number | null>(seedLocationId ?? authStore.homeLocationId ?? null);
  // Location-level records have no userId — pass null ref to useWeeklyRecords
  const nullUserId = ref<string | null>(null);

  const onLocationChange = (value: SelectValue | undefined) => {
    if (!confirmIfDirty()) return;
    selectedLocationId.value = value != null ? Number(value) : null;
  };

  // ── Weekly records ────────────────────────────────────────────────────────
  const {
    weekDates,
    dayAssignmentsMap,
    dayStatusMap,
    daySummaryMap,
    weeklyRegularTotal,
    weeklyOvertimeTotal,
    isDirty,
    markDirty,
    confirmIfDirty,
    isLoading,
    error: loadError,
    loadWeek,
    saveDay,
    navigateWeek,
    createEmptyAssignment,
  } = useWeeklyRecords(
    getSundayOfWeek(seedDate ? DateTime.fromISO(seedDate) : DateTime.now()),
    selectedLocationId,
    nullUserId,
    groupId,
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
    markDirty();
    dayAssignmentsMap.value[selectedDate.value] = [...selectedAssignments.value, createEmptyAssignment(groupId)];
  }

  function removeAssignment(id: string) {
    if (!selectedDate.value) return;
    markDirty();
    const remaining = selectedAssignments.value.filter((a) => a.id !== id);
    dayAssignmentsMap.value[selectedDate.value] = remaining.length > 0 ? remaining : [createEmptyAssignment(groupId)];
  }

  function updateAssignment(updated: DayAssignment) {
    if (!selectedDate.value) return;
    markDirty();
    dayAssignmentsMap.value[selectedDate.value] = selectedAssignments.value.map((a) =>
      a.id === updated.id ? updated : a,
    );
  }

  // ── Validation (no overtime or hours-cap logic) ───────────────────────────
  function validate(assignments: DayAssignment[]): boolean {
    const errors: Record<string, string> = {};
    // Filter out empty placeholder assignments (no category/subcategory selected)
    const realAssignments = assignments.filter((a) => a.categoryId || a.subCategoryId);
    // If all assignments are empty placeholders, allow save (deletes all records for the day)
    if (realAssignments.length === 0) return true;

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
      }

      if (!hasValue && assignment.subCategoryId) {
        errors[`assignment_${i}`] = 'Enter at least one metric value';
      }
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

    isLoadingReference.value = false;

    if (selectedLocationId.value) {
      await loadWeek();
      if (seedDate) {
        onSelectDay(seedDate);
      }
    }
  });

  const weekRangeLabel = computed(() => {
    const dates = weekDates.value;
    if (dates.length < 7) return '';
    const from = DateTime.fromISO(dates[0]);
    const to = DateTime.fromISO(dates[6]);
    return `${from.toFormat('MMM d')} \u2013 ${to.toFormat('MMM d')}, ${to.year}`;
  });

  return {
    accountWarning,
    isLoadingReference,
    isDirty,
    confirmIfDirty,
    groups,
    categories,
    subCategories,
    metrics,
    subCategoryMetrics,
    locationOptions,
    selectedLocationId,
    onLocationChange,
    weekDates,
    dayStatusMap,
    daySummaryMap,
    weeklyRegularTotal,
    weeklyOvertimeTotal,
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
    dayErrors,
    apiError,
    isSaving,
    handleSave,
  };
}
