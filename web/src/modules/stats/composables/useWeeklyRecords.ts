import {
  deleteApiStatsRecordsId,
  getApiStatsRecords,
  postApiStatsRecordsBatch,
  putApiStatsRecordsId,
} from '@/api-access/generated/stat-records/stat-records';
import type { StatRecordResponse, SubCategoryMetricResponse, StatMetricResponse } from '@/api-access/generated/models';
import { computed, ref, type Ref } from 'vue';
import type { DayAssignment, DaySummary } from '../types';

// Returns "YYYY-MM-DD" for a Date
function toISO(date: Date): string {
  return date.toISOString().slice(0, 10);
}

// Returns the Monday of the week containing the given date
export function getMondayOfWeek(date: Date): Date {
  const d = new Date(date);
  const day = d.getDay(); // 0=Sun, 1=Mon, ...
  const diff = day === 0 ? -6 : 1 - day;
  d.setDate(d.getDate() + diff);
  d.setHours(0, 0, 0, 0);
  return d;
}

function isOvertimeMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && (metric.name?.toLowerCase().includes('overtime') ?? false);
}

function isRegularMetric(metric: StatMetricResponse): boolean {
  return metric.unitOfMeasure === 'hours' && !(metric.name?.toLowerCase().includes('overtime') ?? false);
}

let nextId = 1;
function newAssignmentId(): string {
  return String(nextId++);
}

export function useWeeklyRecords(
  weekStart: Ref<string>, // ISO Monday date string
  locationId: Ref<number | null>,
  userId: Ref<string | null>,
  subCategoryMetrics: Ref<SubCategoryMetricResponse[]>,
  metrics: Ref<StatMetricResponse[]>,
) {
  // date string → DayAssignment[]
  const dayAssignmentsMap = ref<Record<string, DayAssignment[]>>({});
  const isLoading = ref(false);
  const error = ref('');

  // 7 ISO date strings Mon–Sun
  const weekDates = computed<string[]>(() => {
    const monday = new Date(weekStart.value + 'T00:00:00');
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(d.getDate() + i);
      return toISO(d);
    });
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
    return daily >= 7 || weeklyRegularTotal.value >= 35;
  }

  // Reconstruct DayAssignment[] from StatRecordResponse[] for one date
  function reconstructAssignments(records: StatRecordResponse[]): DayAssignment[] {
    // Group records by subCategoryId + comment (each group = one row)
    const groups = new Map<string, StatRecordResponse[]>();
    for (const r of records) {
      const scm = subCategoryMetrics.value.find((s) => s.id === r.subCategoryMetricId);
      const subCatId = scm?.subCategoryId ?? 0;
      const key = `${subCatId}::${r.comment ?? ''}`;
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(r);
    }

    const assignments: DayAssignment[] = [];
    for (const groupRecords of groups.values()) {
      const firstRecord = groupRecords[0];
      const scm = subCategoryMetrics.value.find((s) => s.id === firstRecord.subCategoryMetricId);

      const metricValues: Record<number, string> = {};
      const existingRecordIds: Record<number, number> = {};

      for (const r of groupRecords) {
        if (r.subCategoryMetricId != null && r.value != null) {
          metricValues[r.subCategoryMetricId] = String(r.value);
        }
        if (r.subCategoryMetricId != null && r.id != null) {
          existingRecordIds[r.subCategoryMetricId] = r.id;
        }
      }

      assignments.push({
        id: newAssignmentId(),
        groupId: null, // resolved via category in UI
        categoryId: null, // would need a reverse-lookup; populated by subCategoryId
        subCategoryId: scm?.subCategoryId ?? null,
        metricValues,
        existingRecordIds,
        comment: firstRecord.comment ?? '',
      });
    }
    return assignments;
  }

  async function loadWeek(): Promise<void> {
    if (!locationId.value || !userId.value) return;

    isLoading.value = true;
    error.value = '';
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

      // Reconstruct assignments per date
      const newMap: Record<string, DayAssignment[]> = {};
      for (const date of weekDates.value) {
        newMap[date] = reconstructAssignments(byDate[date]);
      }
      dayAssignmentsMap.value = newMap;
    } finally {
      isLoading.value = false;
    }
  }

  async function saveDay(date: string, assignments: DayAssignment[], status: string): Promise<string | null> {
    if (!locationId.value || !userId.value) return 'Missing location or user.';

    // Collect all existing record IDs currently on server for this day
    const existing = dayAssignmentsMap.value[date] ?? [];
    const allExistingIds = new Set<number>();
    for (const a of existing) {
      for (const id of Object.values(a.existingRecordIds)) allExistingIds.add(id);
    }

    const toCreate: Parameters<typeof postApiStatsRecordsBatch>[0] = [];
    const toUpdate: Array<{ id: number; request: Parameters<typeof putApiStatsRecordsId>[1] }> = [];
    const toDelete: number[] = [];

    const seenIds = new Set<number>();

    for (const assignment of assignments) {
      const scms = subCategoryMetrics.value.filter((scm) => scm.subCategoryId === assignment.subCategoryId);

      for (const scm of scms) {
        if (!scm.id) continue;
        const raw = assignment.metricValues[scm.id];
        const val = raw ? parseFloat(raw) : NaN;
        const existingId = assignment.existingRecordIds[scm.id];

        if (existingId != null) seenIds.add(existingId);

        if (!raw || raw.trim() === '' || isNaN(val)) {
          // Empty — delete if existed
          if (existingId != null) toDelete.push(existingId);
          continue;
        }

        const base = {
          dateFrom: date,
          dateTo: date,
          periodType: 'Daily',
          userId: userId.value!,
          locationId: locationId.value!,
          subCategoryMetricId: scm.id,
          value: val,
          comment: assignment.comment || null,
          status,
        };

        if (existingId != null) {
          toUpdate.push({ id: existingId, request: base });
        } else {
          toCreate.push(base);
        }
      }
    }

    // Delete records that existed but are no longer in any assignment
    for (const id of allExistingIds) {
      if (!seenIds.has(id)) toDelete.push(id);
    }

    try {
      await Promise.all([
        ...(toCreate.length > 0 ? [Promise.resolve(postApiStatsRecordsBatch(toCreate))] : []),
        ...toUpdate.map(({ id, request }) => Promise.resolve(putApiStatsRecordsId(id, request))),
        ...toDelete.map((id) => Promise.resolve(deleteApiStatsRecordsId(id))),
      ]);
      await loadWeek();
      return null;
    } catch (e) {
      return e instanceof Error ? e.message : 'Failed to save.';
    }
  }

  function navigateWeek(direction: -1 | 1): void {
    const d = new Date(weekStart.value + 'T00:00:00');
    d.setDate(d.getDate() + direction * 7);
    weekStart.value = toISO(d);
  }

  function createEmptyAssignment(groupId: number | null): DayAssignment {
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
    weekDates,
    dayAssignmentsMap,
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
