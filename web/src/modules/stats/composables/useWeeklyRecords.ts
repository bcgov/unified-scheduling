import { getApiStatsRecords, putApiStatsRecordsDay } from '@/api-access/generated/stat-records/stat-records';
import type {
  StatCategoryResponse,
  StatMetricResponse,
  StatRecordResponse,
  SubCategoryMetricResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import { DateTime } from 'luxon';
import { computed, ref, watch, type Ref } from 'vue';
import type { DayAssignment, DaySummary } from '../types';
import { DAILY_REGULAR_TARGET_HOURS, WEEKLY_REGULAR_TARGET_HOURS } from '../constants';
import { isOvertimeMetric, isRegularMetric } from '../utils/metricHelpers';

// Returns the ISO Monday date string (yyyy-MM-dd) for the week containing the given date
export function getMondayOfWeek(date: DateTime): string {
  return date.startOf('week').toISODate()!;
}

export function useWeeklyRecords(
  initialWeekStart: string, // ISO Monday date string
  locationId: Ref<number | null>,
  userId: Ref<string | null>,
  subCategories: Ref<SubCategoryResponse[]>,
  categories: Ref<StatCategoryResponse[]>,
  subCategoryMetrics: Ref<SubCategoryMetricResponse[]>,
  metrics: Ref<StatMetricResponse[]>,
) {
  // nextId scoped per composable instance to avoid cross-instance leaks
  let nextId = 1;
  function newAssignmentId(): string {
    return String(nextId++);
  }

  const weekStart = ref(initialWeekStart);

  // date string → DayAssignment[]
  const dayAssignmentsMap = ref<Record<string, DayAssignment[]>>({});
  // date string → status ('Draft', 'Submitted', or '' if no records for that date)
  // All records within a day share the same status (enforced by the backend PUT endpoint).
  const dayStatusMap = ref<Record<string, 'Draft' | 'Submitted' | ''>>({});
  const isLoading = ref(false);
  const error = ref('');

  // 7 ISO date strings Mon–Sun
  const weekDates = computed<string[]>(() => {
    const monday = DateTime.fromISO(weekStart.value);
    return Array.from({ length: 7 }, (_, i) => monday.plus({ days: i }).toISODate()!);
  });

  // Helper: get metric by subCategoryMetricId
  function getMetricForScm(scmId: number): StatMetricResponse | undefined {
    const scm = subCategoryMetrics.value.find((s) => s.id === scmId);
    if (!scm) return undefined;
    return metrics.value.find((m) => m.id === scm.metricId);
  }

  // Compute daily summaries from current dayAssignmentsMap
  const daySummaryMap = computed<Record<string, DaySummary>>(() => {
    const result: Record<string, DaySummary> = {};
    for (const date of weekDates.value) {
      const assignments = dayAssignmentsMap.value[date] ?? [];
      let regularHours = 0;
      let overtimeHours = 0;
      for (const a of assignments) {
        for (const [scmIdStr, valStr] of Object.entries(a.metricValues)) {
          const val = parseFloat(valStr);
          if (isNaN(val) || val <= 0) continue;
          const metric = getMetricForScm(Number(scmIdStr));
          if (!metric) continue;
          if (isRegularMetric(metric)) regularHours += val;
          else if (isOvertimeMetric(metric)) overtimeHours += val;
        }
      }
      result[date] = {
        regularHours,
        overtimeHours,
        assignmentCount: assignments.length,
      };
    }
    return result;
  });

  // Weekly regular total across all 7 days
  const weeklyRegularTotal = computed<number>(() =>
    weekDates.value.reduce((sum, d) => sum + (daySummaryMap.value[d]?.regularHours ?? 0), 0),
  );

  const weeklyOvertimeTotal = computed<number>(() =>
    weekDates.value.reduce((sum, d) => sum + (daySummaryMap.value[d]?.overtimeHours ?? 0), 0),
  );

  function isOvertimeEnabled(date: string): boolean {
    const daily = daySummaryMap.value[date]?.regularHours ?? 0;
    return daily >= DAILY_REGULAR_TARGET_HOURS || weeklyRegularTotal.value >= WEEKLY_REGULAR_TARGET_HOURS;
  }

  // Reconstruct DayAssignment[] from StatRecordResponse[] for one date.
  // Records are grouped by subCategoryId + comment. If two records would land
  // in the same group but share the same subCategoryMetricId (duplicate metric),
  // the second record starts a new group to avoid silently merging distinct assignments.
  function reconstructAssignments(records: StatRecordResponse[]): DayAssignment[] {
    // Sort by id for stable, deterministic grouping across reloads
    const sorted = [...records].sort((a, b) => (a.id ?? 0) - (b.id ?? 0));

    type Group = {
      subCatId: number;
      comment: string;
      usedScmIds: Set<number>;
      records: StatRecordResponse[];
    };
    const groups: Group[] = [];

    for (const r of sorted) {
      const scm = subCategoryMetrics.value.find((s) => s.id === r.subCategoryMetricId);
      const subCatId = scm?.subCategoryId ?? 0;
      const comment = r.comment ?? '';
      const scmId = r.subCategoryMetricId ?? -1;

      // Find an existing group with the same subCatId + comment that does not
      // already contain this metric (prevents collision on duplicate assignments)
      const target = groups.find((g) => g.subCatId === subCatId && g.comment === comment && !g.usedScmIds.has(scmId));

      if (target) {
        target.records.push(r);
        target.usedScmIds.add(scmId);
      } else {
        groups.push({ subCatId, comment, usedScmIds: new Set([scmId]), records: [r] });
      }
    }

    return groups.map((group) => {
      const firstRecord = group.records[0];

      // Reverse-lookup categoryId and groupId so reconstructed assignments pass validation
      const subCat = subCategories.value.find((sc) => sc.id === group.subCatId);
      const categoryId = subCat?.categoryId ?? null;
      const category = categories.value.find((c) => c.id === categoryId);
      const groupId = category?.groupId ?? null;

      const metricValues: Record<number, string> = {};
      const existingRecordIds: Record<number, number> = {};

      for (const r of group.records) {
        if (r.subCategoryMetricId != null && r.value != null) {
          metricValues[r.subCategoryMetricId] = String(r.value);
        }
        if (r.subCategoryMetricId != null && r.id != null) {
          existingRecordIds[r.subCategoryMetricId] = r.id;
        }
      }

      return {
        id: newAssignmentId(),
        groupId,
        categoryId,
        subCategoryId: group.subCatId || null,
        metricValues,
        existingRecordIds,
        comment: firstRecord.comment ?? '',
      };
    });
  }

  async function loadWeek(): Promise<void> {
    if (!locationId.value || !userId.value) return;

    isLoading.value = true;
    error.value = '';
    dayAssignmentsMap.value = {};
    dayStatusMap.value = {};
    try {
      const from = weekDates.value[0];
      const to = weekDates.value[6];

      const { data, error: apiError } = await getApiStatsRecords({
        LocationId: locationId.value,
        FromDate: from,
        ToDate: to,
        PeriodType: 'Daily',
        UserId: userId.value,
      });

      if (apiError.value) {
        error.value = apiError.value.message ?? 'Failed to load records.';
        dayAssignmentsMap.value = {};
        return;
      }

      const records = data.value ?? [];

      // Group by date
      const byDate: Record<string, StatRecordResponse[]> = {};
      for (const date of weekDates.value) byDate[date] = [];
      for (const r of records) {
        if (r.dateFrom && byDate[r.dateFrom] !== undefined) {
          byDate[r.dateFrom].push(r);
        }
      }

      // Reconstruct assignments and status per date
      const newMap: Record<string, DayAssignment[]> = {};
      const newStatusMap: Record<string, string> = {};
      for (const date of weekDates.value) {
        newMap[date] = reconstructAssignments(byDate[date]);
        const s = byDate[date][0]?.status;
        newStatusMap[date] = s === 'Draft' || s === 'Submitted' ? s : '';
      }
      dayAssignmentsMap.value = newMap;
      dayStatusMap.value = newStatusMap;
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load records.';
      dayAssignmentsMap.value = {};
    } finally {
      isLoading.value = false;
    }
  }

  // Reload whenever the week, location, or user changes
  watch([weekStart, locationId, userId], () => loadWeek());

  async function saveDay(date: string, assignments: DayAssignment[], status: string): Promise<string | null> {
    if (!locationId.value || !userId.value) return 'Missing location or user.';

    // Build the desired final state for the day. The backend applies all
    // creates/updates/deletes in a single transaction.
    const records = assignments.flatMap((assignment) => {
      const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);
      return scms.flatMap((scm) => {
        if (!scm.id) return [];
        const raw = assignment.metricValues[scm.id];
        const val = raw ? parseFloat(raw) : NaN;
        if (!raw || raw.trim() === '' || isNaN(val)) return [];
        return [
          {
            id: assignment.existingRecordIds[scm.id] ?? null,
            subCategoryMetricId: scm.id,
            value: val,
            comment: assignment.comment || null,
          },
        ];
      });
    });

    const { error: apiError } = await putApiStatsRecordsDay({
      date,
      locationId: locationId.value,
      userId: userId.value,
      status,
      records,
    });

    if (apiError.value) {
      return apiError.value.message ?? 'Failed to save.';
    }

    await loadWeek();
    return null;
  }

  function navigateWeek(direction: -1 | 1): void {
    weekStart.value = DateTime.fromISO(weekStart.value).plus({ weeks: direction }).toISODate()!;
  }

  function createEmptyAssignment(groupId: number): DayAssignment {
    return {
      id: newAssignmentId(),
      groupId,
      categoryId: null,
      subCategoryId: null,
      metricValues: {},
      existingRecordIds: {},
      comment: '',
    };
  }

  return {
    weekStart,
    weekDates,
    dayAssignmentsMap,
    dayStatusMap,
    daySummaryMap,
    weeklyRegularTotal,
    weeklyOvertimeTotal,
    isOvertimeEnabled,
    isLoading,
    error,
    loadWeek,
    saveDay,
    navigateWeek,
    createEmptyAssignment,
  };
}
