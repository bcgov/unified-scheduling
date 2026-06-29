import type {
  DashboardEntryResponse,
  DashboardSummaryResponse,
  StatCategoryResponse,
  StatGroupResponse,
  SubCategoryResponse,
} from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import {
  getApiStatsDashboardEntries,
  getApiStatsDashboardSummary,
  postApiStatsDashboardSignOff,
} from '@/api-access/generated/dashboard/dashboard';
import { getApiStatsCategories } from '@/api-access/generated/stat-categories/stat-categories';
import { getApiStatsGroups } from '@/api-access/generated/stat-groups/stat-groups';
import { getApiStatsSubCategories } from '@/api-access/generated/sub-categories/sub-categories';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import { useAuthStore } from '@/stores/auth';
import { useLocationsStore } from '@/stores/LocationsStore';
import { DateTime } from 'luxon';
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { EntryStatus, GROUP_ROUTE } from '../constants';
import { exportToCsv } from '../utils/exportCsv';

export function useStatSearch() {
  const authStore = useAuthStore();
  const locationsStore = useLocationsStore();
  const { hasPermission } = useAccessControl();
  const router = useRouter();
  const route = useRoute();

  // ── Permissions ────────────────────────────────────────────────────────
  const canViewDashboard = computed(() => hasPermission(Permissions.DashboardView));
  const canEditEntries = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));
  const canSignOff = computed(() => hasPermission(Permissions.DashboardSignOff));

  // ── Reference data ──────────────────────────────────────────────────────
  const locationUsers = ref<{ id: string; name: string }[]>([]);
  const groups = ref<StatGroupResponse[]>([]);
  const categories = ref<StatCategoryResponse[]>([]);
  const subCategories = ref<SubCategoryResponse[]>([]);
  const isLoadingReference = ref(false);

  // ── Filter state (pre-seeded from query params when present) ─────────────
  const groupId = ref<number | null>(null);
  const employeeId = ref<string | null>(null);
  const qLocationId = Number(route.query.locationId);
  const locationId = ref<number | null>(Number.isFinite(qLocationId) ? qLocationId : null);
  const categoryName = ref<string | null>(null);
  const subCategoryId = ref<number | null>(null);
  const status = ref<string | null>((route.query.status as string) || null);
  const monday = DateTime.now().startOf('week');
  const sunday = monday.plus({ days: 6 });
  const fromDate = ref<string | null>((route.query.fromDate as string) || monday.toISODate());
  const toDate = ref<string | null>((route.query.toDate as string) || sunday.toISODate());

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

  const locationOptions = computed(() => locationsStore.selectOptions);

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

  // ── Table columns ───────────────────────────────────────────────────────
  const columns = computed(() => {
    const cols = [
      { title: 'Employee', key: 'employeeName', sortable: true },
      { title: 'Date', key: 'date', sortable: true },
      { title: 'Work Area', key: 'workArea', sortable: true },
      { title: 'Subcategory', key: 'subcategory', sortable: true },
      { title: 'Metric', key: 'metricName', sortable: true },
      { title: 'Unit', key: 'metricUnit', sortable: true },
      { title: 'Value', key: 'value', sortable: true },
      { title: 'Status', key: 'status', sortable: true },
    ];
    if (canEditEntries.value) cols.push({ title: '', key: 'actions', sortable: false });
    return cols;
  });

  // ── Row selection ───────────────────────────────────────────────────────
  const selectedItems = ref<DashboardEntryResponse[]>([]);

  // ── Sign-off state ──────────────────────────────────────────────────────
  const showSignOffModal = ref(false);
  const isSigningOff = ref(false);
  const signOffError = ref('');

  const selectedGroupName = computed(() => {
    if (!groupId.value) return '';
    return groups.value.find((g) => g.id === groupId.value)?.name ?? '';
  });

  const canInitiateSignOff = computed(() => canSignOff.value && selectedItems.value.length > 0);

  // ── Data loading ────────────────────────────────────────────────────────
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
    selectedItems.value = [];
    await Promise.all([loadEntries(), loadSummary()]);
  }

  // ── Sign-off actions ────────────────────────────────────────────────────
  async function confirmSignOff(entryIds: number[]) {
    isSigningOff.value = true;
    signOffError.value = '';
    try {
      const res = await postApiStatsDashboardSignOff({ entryIds });
      if (res.error.value) {
        signOffError.value = res.error.value.message ?? 'Sign-off failed.';
        return;
      }
      showSignOffModal.value = false;
      selectedItems.value = [];
      await applyFilters();
    } catch (e) {
      signOffError.value = e instanceof Error ? e.message : 'Sign-off failed.';
    } finally {
      isSigningOff.value = false;
    }
  }

  // ── Edit navigation ─────────────────────────────────────────────────────
  function openEdit(item: DashboardEntryResponse) {
    const itemGroupId = item.groupId;
    const itemLocationId = item.locationId;
    if (!item.userId || !item.date || !itemGroupId || !itemLocationId) {
      error.value = 'Unable to open entry for editing — missing data.';
      return;
    }

    const routeName = GROUP_ROUTE[itemGroupId];
    if (!routeName) {
      error.value = 'Unable to open entry for editing — unknown work area group.';
      return;
    }

    const url = router.resolve({
      name: routeName,
      query: {
        userId: item.userId,
        locationId: String(itemLocationId),
        date: item.date,
        employeeName: item.employeeName ?? '',
      },
    }).href;
    window.open(url, '_blank');
  }

  // ── CSV Export ──────────────────────────────────────────────────────────
  function exportEntriesCsv() {
    const headers = ['Employee', 'Date', 'Work Area', 'Subcategory', 'Metric', 'Unit', 'Value', 'Status'];
    const rows = entries.value.map((e) => [
      e.employeeName ?? '',
      e.date ?? '',
      e.workArea ?? '',
      e.subcategory ?? '',
      e.metricName ?? '',
      e.metricUnit ?? '',
      String(e.value ?? ''),
      e.status === EntryStatus.SignedOff ? 'Signed Off' : (e.status ?? ''),
    ]);
    const datePart = fromDate.value && toDate.value ? `_${fromDate.value}_to_${toDate.value}` : '';
    exportToCsv(`dashboard_entries${datePart}.csv`, headers, rows);
  }

  return {
    // Permissions
    canViewDashboard,
    canEditEntries,
    canSignOff,
    // Reference data
    groups,
    employees,
    categories,
    subCategories,
    isLoadingReference,
    // Filters
    groupId,
    employeeId,
    locationId,
    categoryName,
    subCategoryId,
    status,
    fromDate,
    toDate,
    locationOptions,
    // Table
    columns,
    entries,
    isLoadingEntries,
    selectedItems,
    // Summary
    summary,
    isLoadingSummary,
    // Sign-off
    showSignOffModal,
    isSigningOff,
    signOffError,
    selectedGroupName,
    canInitiateSignOff,
    // Errors
    error,
    // Actions
    loadReferenceData,
    applyFilters,
    confirmSignOff,
    openEdit,
    exportEntriesCsv,
  };
}
