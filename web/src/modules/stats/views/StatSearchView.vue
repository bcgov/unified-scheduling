<script setup lang="ts">
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { computed } from 'vue';
import DashboardFilters from '../components/DashboardFilters.vue';
import { useStatSearch } from '../composables/useStatSearch';

const { hasPermission } = useAccessControl();
const canViewDashboard = computed(() => hasPermission(Permissions.DashboardView));
const canSignOff = computed(() => hasPermission(Permissions.DashboardSignOff));
const canSubmit = computed(() => hasPermission(Permissions.DashboardSubmit));

const locationsStore = useLocationsStore();

const {
  employees,
  categories,
  subCategories,
  isLoadingEntries,
  entries,
  locationId,
  employeeId,
  categoryId,
  subCategoryId,
  status,
  loadEntries,
} = useStatSearch();

const columns = [
  { title: 'Employee', key: 'employeeName', sortable: true },
  { title: 'Date', key: 'date', sortable: true },
  { title: 'Work Area', key: 'workArea', sortable: true },
  { title: 'Subcategory', key: 'subcategory', sortable: true },
  { title: 'Metric', key: 'metric', sortable: true },
  { title: 'Value', key: 'value', sortable: true },
  { title: 'Status', key: 'status', sortable: true },
];

function statusColor(s: string) {
  return s === 'Submitted' ? 'success' : 'warning';
}
</script>

<template>
  <div v-if="!canViewDashboard" class="no-access">
    <p>You do not have permission to view this page.</p>
  </div>

  <div v-else class="search-view">
    <h2 class="page-title">Search / View / Edit Data</h2>

    <!-- Main layout -->
    <div class="search-layout">
      <!-- Entries panel -->
      <div class="panel">
        <h3 class="panel-title">Entries</h3>

        <UaDataTable :headers="columns" :items="entries" :loading="isLoadingEntries" item-value="id" hover>
          <template #item.status="{ item }">
            <v-chip :color="statusColor(item.status)" size="small" variant="tonal">
              {{ item.status }}
            </v-chip>
          </template>

          <template #no-data>
            <span class="no-data-text">No entries found.</span>
          </template>
        </UaDataTable>

        <div v-if="canSignOff || canSubmit" class="entries-actions">
          <UaBtn v-if="canSignOff" variant="outlined">Sign Off</UaBtn>
          <UaBtn v-if="canSubmit" color="primary" variant="flat">Submit</UaBtn>
        </div>
      </div>

      <!-- Filters panel -->
      <DashboardFilters
        v-model:location-id="locationId"
        v-model:employee-id="employeeId"
        v-model:category-id="categoryId"
        v-model:sub-category-id="subCategoryId"
        v-model:status="status"
        :locations="locationsStore.entities"
        :employees="employees"
        :categories="categories"
        :sub-categories="subCategories"
        :loading="isLoadingEntries"
        @apply="loadEntries"
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
  justify-content: space-between;
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
