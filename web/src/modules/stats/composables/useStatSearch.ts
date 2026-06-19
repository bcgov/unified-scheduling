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
import { onMounted, ref } from 'vue';

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

  // ── Table data ──────────────────────────────────────────────────────────
  const entries = ref<DashboardEntryResponse[]>([]);
  const isLoadingEntries = ref(false);
  const error = ref('');

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

  onMounted(async () => {
    await Promise.all([loadReferenceData(), loadEntries()]);
  });

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
    entries,
    isLoadingEntries,
    error,
    loadEntries,
  };
}
