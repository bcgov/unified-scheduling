import type {
  DashboardEntryResponse,
  DashboardSummaryResponse,
  StatCategoryResponse,
  StatGroupResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import {
  getApiStatsDashboardEntries,
  getApiStatsDashboardSummary,
  postApiStatsDashboardSignOff,
} from '@/api-access/generated/dashboard/dashboard';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsGroups } from '@/api-access/generated/stat-groups/stat-groups';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import { DateTime } from 'luxon';
import { computed, ref } from 'vue';

export function useStatSearch() {
  const authStore = useAuthStore();
  const locationsStore = useLocationsStore();

  // ── Reference data ──────────────────────────────────────────────────────
  const locationUsers = ref<{ id: string; name: string }[]>([]);
  const groups = ref<StatGroupResponse[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const isLoadingReference = ref(false);

  // ── Filter state ────────────────────────────────────────────────────────
  const groupId = ref<number | null>(null);
  const employeeId = ref<string | null>(null);
  const locationId = ref<number | null>(null);
  const categoryName = ref<string | null>(null);
  const subCategoryId = ref<number | null>(null);
  const status = ref<string | null>(null);
  const monday = DateTime.now().startOf('week');
  const sunday = monday.plus({ days: 6 });
  const fromDate = ref<string | null>(monday.toISODate());
  const toDate = ref<string | null>(sunday.toISODate());

  // ── Table data ──────────────────────────────────────────────────────────
  const entries = ref<DashboardEntryResponse[]>([]);
  const isLoadingEntries = ref(false);

  // ── Summary stats ───────────────────────────────────────────────────────
  const summary = ref<DashboardSummaryResponse>({
    regularHours: 0,
    overtimeHours: 0,
    submittedCount: 0,
    totalEntries: 0,
  });
  const isLoadingSummary = ref(false);

  const error = ref('');

  // Merge location users with any additional employees found in entries (e.g. loaned staff)
  const employees = computed(() => {
    const map = new Map<string, string>();
    for (const u of locationUsers.value) {
      map.set(u.id, u.name);
    }
    for (const e of entries.value) {
      if (e.userId && e.employeeName && !map.has(e.userId)) {
        map.set(e.userId, e.employeeName);
      }
    }
    return Array.from(map, ([id, name]) => ({ id, name }));
  });

  async function loadReferenceData() {
    isLoadingReference.value = true;
    try {
      const [groupsRes, catsRes, subCatsRes, usersRes] = await Promise.all([
        getApiStatsGroups(),
        getApiStatsCategories(),
        getApiStatsSubCategories(),
        getApiUsers({ LocationId: authStore.homeLocationId ?? undefined, IsEnabled: true }),
        locationsStore.getEntities(),
      ]);
      if (groupsRes.error.value) {
        error.value = groupsRes.error.value.message ?? 'Failed to load groups.';
        return;
      }
      if (catsRes.error.value) {
        error.value = catsRes.error.value.message ?? 'Failed to load categories.';
        return;
      }
      if (subCatsRes.error.value) {
        error.value = subCatsRes.error.value.message ?? 'Failed to load subcategories.';
        return;
      }
      if (usersRes.error.value) {
        error.value = usersRes.error.value.message ?? 'Failed to load users.';
        return;
      }
      groups.value = groupsRes.data.value ?? [];
      categories.value = catsRes.data.value ?? [];
      subCategories.value = subCatsRes.data.value ?? [];
      locationUsers.value = (usersRes.data.value ?? [])
        .filter((u) => u.id != null)
        .map((u) => ({ id: u.id!, name: `${u.firstName} ${u.lastName}`.trim() }));
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load reference data.';
    } finally {
      isLoadingReference.value = false;
    }
  }

  function buildQueryParams() {
    return {
      GroupId: groupId.value ?? undefined,
      EmployeeId: employeeId.value ?? undefined,
      LocationId: locationId.value ?? undefined,
      CategoryName: categoryName.value ?? undefined,
      SubCategoryId: subCategoryId.value ?? undefined,
      Status: status.value ?? undefined,
      FromDate: fromDate.value ?? undefined,
      ToDate: toDate.value ?? undefined,
    };
  }

  async function loadEntries() {
    isLoadingEntries.value = true;
    error.value = '';
    try {
      const res = await getApiStatsDashboardEntries(buildQueryParams());
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load entries.';
        return;
      }
      entries.value = res.data.value ?? [];
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load entries.';
    } finally {
      isLoadingEntries.value = false;
    }
  }

  async function loadSummary() {
    isLoadingSummary.value = true;
    try {
      const res = await getApiStatsDashboardSummary(buildQueryParams());
      if (res.error.value) {
        error.value = res.error.value.message ?? 'Failed to load summary.';
        return;
      }
      summary.value = res.data.value ?? summary.value;
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load summary.';
    } finally {
      isLoadingSummary.value = false;
    }
  }

  async function applyFilters() {
    await Promise.all([loadEntries(), loadSummary()]);
  }

  async function signOff(entryIds: number[]): Promise<string | null> {
    error.value = '';
    try {
      const res = await postApiStatsDashboardSignOff({ entryIds });
      if (res.error.value) {
        return res.error.value.message ?? 'Sign-off failed.';
      }
      return null;
    } catch (e) {
      return e instanceof Error ? e.message : 'Sign-off failed.';
    }
  }

  return {
    groups,
    employees,
    categories,
    subCategories,
    isLoadingReference,
    groupId,
    employeeId,
    locationId,
    categoryName,
    subCategoryId,
    status,
    fromDate,
    toDate,
    entries,
    isLoadingEntries,
    summary,
    isLoadingSummary,
    error,
    loadReferenceData,
    applyFilters,
    signOff,
    locationsStore,
  };
}
