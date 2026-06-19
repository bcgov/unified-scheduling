import type { DashboardEntryResponse, StatCategoryResponse, SubCategoryResponse } from '@/api-access/generated/models';
import { getApiStatsDashboardEntries } from '@/api-access/generated/dashboard/dashboard';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAuthStore } from '@/stores/auth';
import { DateTime } from 'luxon';
import { computed, ref } from 'vue';
import { EntryStatus } from '../constants';

export function useStatSearch() {
  const authStore = useAuthStore();

  // ── Reference data ──────────────────────────────────────────────────────
  const locationUsers = ref<{ id: string; name: string }[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const isLoadingReference = ref(false);

  // ── Filter state ────────────────────────────────────────────────────────
  const employeeId = ref<string | null>(null);
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
  const error = ref('');

  // ── Summary stats (client-side, placeholder until SS-956 adds a backend endpoint) ──
  const regularHours = computed(() =>
    entries.value
      .filter((e) => e.metricUnit?.toLowerCase() === 'hours' && !e.isOvertime)
      .reduce((sum, e) => sum + (e.value ?? 0), 0),
  );

  const overtimeHours = computed(() =>
    entries.value.filter((e) => e.isOvertime).reduce((sum, e) => sum + (e.value ?? 0), 0),
  );

  const submittedCount = computed(() => entries.value.filter((e) => e.status === EntryStatus.Submitted).length);

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
      const [catsRes, subCatsRes, usersRes] = await Promise.all([
        getApiStatsCategories(),
        getApiStatsSubCategories(),
        getApiUsers({ LocationId: authStore.homeLocationId ?? undefined, IsEnabled: true }),
      ]);
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

  async function loadEntries() {
    isLoadingEntries.value = true;
    error.value = '';
    try {
      const res = await getApiStatsDashboardEntries({
        EmployeeId: employeeId.value ?? undefined,
        CategoryName: categoryName.value ?? undefined,
        SubCategoryId: subCategoryId.value ?? undefined,
        Status: status.value ?? undefined,
        FromDate: fromDate.value ?? undefined,
        ToDate: toDate.value ?? undefined,
      });
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

  return {
    employees,
    categories,
    subCategories,
    isLoadingReference,
    employeeId,
    categoryName,
    subCategoryId,
    status,
    fromDate,
    toDate,
    entries,
    isLoadingEntries,
    error,
    regularHours,
    overtimeHours,
    submittedCount,
    loadReferenceData,
    loadEntries,
  };
}
