<script setup lang="ts">
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import { mdiDownload, mdiPencil } from '@mdi/js';
import { onMounted } from 'vue';
import DashboardFilters from '../components/DashboardFilters.vue';
import SignOffConfirmModal from '../components/SignOffConfirmModal.vue';
import { EntryStatus, statusColor } from '../constants';
import { useStatSearch } from '../composables/useStatSearch';
import type { DashboardEntryResponse } from '@/api-access/generated/models';

const {
  // Permissions
  canViewDashboard,
  canEditEntries,
  canSignOff,
  // Reference data
  groups,
  employees,
  categories,
  subCategories,
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
} = useStatSearch();

onMounted(async () => {
  if (!canViewDashboard.value) return;
  await Promise.all([loadReferenceData(), applyFilters()]);
});
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
        <div class="panel-header">
          <h3 class="panel-title">Entries</h3>
          <UaBtn
            variant="outlined"
            size="small"
            :prepend-icon="mdiDownload"
            :disabled="entries.length === 0"
            @click="exportEntriesCsv"
          >
            Export CSV
          </UaBtn>
        </div>

        <UaDataTable
          v-model="selectedItems"
          :headers="columns"
          :items="entries"
          :loading="isLoadingEntries"
          :item-selectable="(item: DashboardEntryResponse) => item.status !== EntryStatus.SignedOff"
          item-value="id"
          return-object
          show-select
          hover
        >
          <template #[`item.status`]="{ item }">
            <v-chip :color="statusColor(item.status)" size="small" variant="tonal">
              {{ item.status === EntryStatus.SignedOff ? 'Signed Off' : item.status }}
            </v-chip>
          </template>

          <template v-if="canEditEntries" #[`item.actions`]="{ item }">
            <v-btn
              v-if="item.status !== EntryStatus.SignedOff"
              icon
              variant="text"
              size="small"
              @click="openEdit(item)"
            >
              <v-icon :icon="mdiPencil" />
            </v-btn>
          </template>

          <template #no-data>
            <span class="no-data-text">No entries found.</span>
          </template>
        </UaDataTable>

        <div v-if="canSignOff" class="entries-actions">
          <UaAlert v-if="signOffError" type="error" class="signoff-error">{{ signOffError }}</UaAlert>
          <p v-if="selectedItems.length === 0" class="signoff-hint">Select one or more entries to sign off.</p>
          <UaBtn
            color="primary"
            variant="flat"
            :disabled="!canInitiateSignOff"
            :loading="isSigningOff"
            @click="showSignOffModal = true"
          >
            Sign Off
          </UaBtn>
        </div>
      </div>

      <!-- Filters panel -->
      <DashboardFilters
        v-model:group-id="groupId"
        v-model:employee-id="employeeId"
        v-model:location-id="locationId"
        v-model:category-name="categoryName"
        v-model:sub-category-id="subCategoryId"
        v-model:status="status"
        v-model:from-date="fromDate"
        v-model:to-date="toDate"
        :groups="groups"
        :employees="employees"
        :locations="locationOptions"
        :categories="categories"
        :sub-categories="subCategories"
        :loading="isLoadingEntries"
        @apply="applyFilters"
      />
    </div>

    <SignOffConfirmModal
      v-if="showSignOffModal"
      :selected-entries="selectedItems"
      :group-name="selectedGroupName"
      @confirm="confirmSignOff"
      @close="showSignOffModal = false"
    />
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

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
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
  flex-direction: column;
  gap: var(--ua-spacing-sm);
  padding-top: var(--ua-spacing-sm);
  border-top: 1px solid var(--ua-border-color);
  align-items: flex-end;
}

.signoff-error {
  width: 100%;
}

.signoff-hint {
  margin: 0;
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
  text-align: right;
}

.no-data-text {
  color: var(--ua-text-secondary);
}

.no-access {
  padding: var(--ua-spacing-xl);
  color: var(--ua-text-secondary);
}
</style>
