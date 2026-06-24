<script setup lang="ts">
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import { useAuthStore } from '@/stores/auth';
import { mdiPencil } from '@mdi/js';
import { computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import DashboardFilters from '../components/DashboardFilters.vue';
import { EntryStatus } from '../constants';
import { useStatSearch } from '../composables/useStatSearch';

const { hasPermission } = useAccessControl();
const canViewDashboard = computed(() => hasPermission(Permissions.DashboardView));
const canSubmit = computed(() => hasPermission(Permissions.DashboardSubmit));
const canEditEntries = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));
const authStore = useAuthStore();
const router = useRouter();

const {
  employees,
  categories,
  subCategories,
  isLoadingEntries,
  entries,
  employeeId,
  categoryName,
  subCategoryId,
  status,
  fromDate,
  toDate,
  error,
  summary,
  loadReferenceData,
  applyFilters,
} = useStatSearch();

onMounted(async () => {
  if (!canViewDashboard.value) return;
  await Promise.all([loadReferenceData(), applyFilters()]);
});

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

function statusColor(s: string | undefined) {
  if (s === EntryStatus.Submitted) return 'success';
  if (s === EntryStatus.Draft) return 'warning';
  return 'default';
}

// ── Edit: open in new tab pre-seeded with the entry's user/date/location ───
const GROUP_ROUTE: Record<number, string> = { 1: 'NonSupervisionForm', 2: 'SupervisionForm' };

function openEdit(item: (typeof entries.value)[number]) {
  const category = categories.value.find((c) => c.name === item.workArea);
  const groupId = category?.groupId;
  const locationId = authStore.homeLocationId;
  if (!item.userId || !item.date || !groupId || !locationId) {
    error.value = 'Unable to open entry for editing — missing data.';
    return;
  }

  const routeName = GROUP_ROUTE[groupId];
  if (!routeName) {
    error.value = 'Unable to open entry for editing — unknown work area group.';
    return;
  }

  const url = router.resolve({
    name: routeName,
    query: { userId: item.userId, locationId: String(locationId), date: item.date },
  }).href;
  window.open(url, '_blank');
}
</script>

<template>
  <div v-if="!canViewDashboard" class="no-access">
    <p>You do not have permission to view this page.</p>
  </div>

  <div v-else class="search-view">
    <h2 class="page-title">Search / View / Edit Data</h2>

    <UaAlert v-if="error" type="error">{{ error }}</UaAlert>

    <!-- Summary cards -->
    <div class="summary-grid">
      <div class="summary-card">
        <span class="summary-value">{{ summary.regularHours ?? 0 }}</span>
        <span class="summary-label">Regular Hours</span>
      </div>
      <div class="summary-card">
        <span class="summary-value">{{ summary.overtimeHours ?? 0 }}</span>
        <span class="summary-label">Overtime Hours</span>
      </div>
      <div class="summary-card">
        <span class="summary-value">{{ summary.submittedCount ?? 0 }}</span>
        <span class="summary-label">Submitted</span>
      </div>
      <div class="summary-card">
        <span class="summary-value">{{ summary.totalEntries ?? 0 }}</span>
        <span class="summary-label">Total Entries</span>
      </div>
    </div>

    <!-- Main layout -->
    <div class="search-layout">
      <!-- Entries panel -->
      <div class="panel">
        <h3 class="panel-title">Entries</h3>

        <UaDataTable :headers="columns" :items="entries" :loading="isLoadingEntries" item-value="id" hover>
          <template #[`item.status`]="{ item }">
            <v-chip :color="statusColor(item.status)" size="small" variant="tonal">
              {{ item.status }}
            </v-chip>
          </template>

          <template v-if="canEditEntries" #[`item.actions`]="{ item }">
            <v-btn icon variant="text" size="small" @click="openEdit(item)">
              <v-icon :icon="mdiPencil" />
            </v-btn>
          </template>

          <template #no-data>
            <span class="no-data-text">No entries found.</span>
          </template>
        </UaDataTable>

        <div v-if="canSubmit" class="entries-actions">
          <UaBtn color="primary" variant="flat">Submit</UaBtn>
        </div>
      </div>

      <!-- Filters panel -->
      <DashboardFilters
        v-model:employee-id="employeeId"
        v-model:category-name="categoryName"
        v-model:sub-category-id="subCategoryId"
        v-model:status="status"
        v-model:from-date="fromDate"
        v-model:to-date="toDate"
        :employees="employees"
        :categories="categories"
        :sub-categories="subCategories"
        :loading="isLoadingEntries"
        @apply="applyFilters"
      />
    </div>
  </div>
</template>

<style scoped>
.search-view {
  padding: var(--ua-spacing-xl);
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
}

.page-title {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin: 0;
}

/* ── Summary cards ─────────────────────────────────────────────────── */
.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: var(--ua-spacing-md);
}

.summary-card {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  padding: var(--ua-spacing-lg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: rgb(var(--v-theme-surface));
}

.summary-value {
  font-size: var(--ua-font-size-2xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  line-height: 1;
}

.summary-label {
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
}

/* ── Main layout ───────────────────────────────────────────────────── */
.search-layout {
  display: grid;
  grid-template-columns: 1fr 280px;
  gap: var(--ua-spacing-lg);
  align-items: start;
}

/* ── Entries panel ─────────────────────────────────────────────────── */
.panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-lg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: rgb(var(--v-theme-surface));
}

.panel-title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin: 0;
  white-space: nowrap;
}

.entries-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ua-spacing-sm);
  padding-top: var(--ua-spacing-sm);
  border-top: 1px solid var(--ua-border-color);
}

.no-data-text {
  color: var(--ua-text-secondary);
}

.no-access {
  padding: var(--ua-spacing-xl);
  color: var(--ua-text-secondary);
}
</style>
