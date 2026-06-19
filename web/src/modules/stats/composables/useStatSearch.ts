import type {
  DashboardEntryResponse,
  StatCategoryResponse,
  SubCategoryResponse,
  UserResponse,
} from '@/api-access/generated/models';
import { getApiStatsDashboardEntries } from '@/api-access/generated/dashboard/dashboard';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiUsers } from '@/api-access/generated/users/users';
import { DateTime } from 'luxon';
import { computed, ref } from 'vue';
import { EntryStatus } from '../constants';

export function useStatSearch() {
  // ── Reference data ──────────────────────────────────────────────────────
  const employees = ref<UserResponse[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const isLoadingReference = ref(false);

  // ── Filter state ────────────────────────────────────────────────────────
  const locationId = ref<number | null>(null);
  const employeeId = ref<string | null>(null);
  const categoryId = ref<number | null>(null);
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
      .filter((e) => {
        const m = e.metric?.toLowerCase() ?? '';
        return m.includes('hours') && !m.includes('overtime');
      })
      .reduce((sum, e) => sum + (e.value ?? 0), 0),
  );

  const overtimeHours = computed(() =>
    entries.value
      .filter((e) => e.metric?.toLowerCase().includes('overtime'))
      .reduce((sum, e) => sum + (e.value ?? 0), 0),
  );

  const submittedCount = computed(() => entries.value.filter((e) => e.status === EntryStatus.Submitted).length);

  async function loadReferenceData() {
    isLoadingReference.value = true;
    try {
      const [catsRes, subCatsRes, usersRes] = await Promise.all([
        getApiStatsCategories(),
        getApiStatsSubCategories(),
        getApiUsers({ IsEnabled: true }),
      ]);
      categories.value = catsRes.data.value ?? [];
      subCategories.value = subCatsRes.data.value ?? [];
      employees.value = usersRes.data.value ?? [];
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
        CategoryId: categoryId.value ?? undefined,
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
    locationId,
    employeeId,
    categoryId,
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
