<script setup lang="ts">
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import { mdiPencil } from '@mdi/js';
import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import DashboardFilters from '../components/DashboardFilters.vue';
import SignOffConfirmModal from '../components/SignOffConfirmModal.vue';
import { EntryStatus, GROUP_ROUTE } from '../constants';
import { useStatSearch } from '../composables/useStatSearch';
import type { DashboardEntryResponse } from '@/api-access/generated/models';

const { hasPermission } = useAccessControl();
const canViewDashboard = computed(() => hasPermission(Permissions.DashboardView));
const canEditEntries = computed(() => hasPermission(Permissions.StatsRecordsEnterForOthers));
const canSignOff = computed(() => hasPermission(Permissions.DashboardSignOff));
const router = useRouter();

const {
  groups,
  employees,
  categories,
  subCategories,
  isLoadingEntries,
  entries,
  groupId,
  employeeId,
  locationId,
  categoryName,
  subCategoryId,
  status,
  fromDate,
  toDate,
  error,
  summary,
  locationOptions,
  loadReferenceData,
  applyFilters,
  signOff,
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

const STATUS_COLORS: Partial<Record<string, string>> = {
  [EntryStatus.SignedOff]: 'primary',
  [EntryStatus.Submitted]: 'success',
  [EntryStatus.Draft]: 'warning',
};

function statusColor(status?: string) {
  return status ? (STATUS_COLORS[status] ?? 'default') : 'default';
}

// ── Edit: open in new tab pre-seeded with the entry's user/date/location ───
function openEdit(item: (typeof entries.value)[number]) {
  const itemGroupId = item.groupId;
  const locationId = item.locationId;
  if (!item.userId || !item.date || !itemGroupId || !locationId) {
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
      locationId: String(locationId),
      date: item.date,
      employeeName: item.employeeName ?? '',
    },
  }).href;
  window.open(url, '_blank');
}

// ── Row selection ───────────────────────────────────────────────────────────
const selectedItems = ref<DashboardEntryResponse[]>([]);

// ── Sign-off ────────────────────────────────────────────────────────────────
const showSignOffModal = ref(false);
const isSigningOff = ref(false);
const signOffError = ref('');

const selectedGroupName = computed(() => {
  if (!groupId.value) return '';
  return groups.value.find((g) => g.id === groupId.value)?.name ?? '';
});

const canInitiateSignOff = computed(() => canSignOff.value && groupId.value != null && selectedItems.value.length > 0);

async function onSignOffConfirm(entryIds: number[]) {
  isSigningOff.value = true;
  signOffError.value = '';
  const err = await signOff(entryIds);
  isSigningOff.value = false;
  if (err) {
    signOffError.value = err;
    return;
  }
  showSignOffModal.value = false;
  selectedItems.value = [];
  await applyFilters();
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
          <p v-if="!groupId" class="signoff-hint">Select a group in the Filters panel to enable sign off.</p>
          <p v-else-if="selectedItems.length === 0" class="signoff-hint">Select one or more entries to sign off.</p>
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
        @apply="
          () => {
            selectedItems = [];
            applyFilters();
          }
        "
      />
    </div>

    <SignOffConfirmModal
      v-if="showSignOffModal"
      :selected-entries="selectedItems"
      :group-name="selectedGroupName"
      @confirm="onSignOffConfirm"
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
