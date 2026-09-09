<script setup lang="ts">
import { computed, ref } from 'vue';
import { Permissions } from '@/api-access/generated/models';
import { getApiLocationAll } from '@/api-access/generated/location/location';
import { getApiRegion } from '@/api-access/generated/region/region';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import type { SelectOption } from '@/types/select';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { useTrainingLookup } from '@/modules/training/trainingLookupApi';
import { useUserTrainingReport, type UserTrainingReportItem } from './userTrainingReportApi';
import { exportReportToCsv, type ReportCsvColumn } from './reportCsvExport';

const accessControl = useAccessControl();
const canAccessUserTrainingReport = computed(() => accessControl.hasPermission(Permissions.ReportsGenerate));

const statusFilter = ref<'all' | 'active' | 'expired' | 'notTaken'>('all');
const selectedUserIdFilter = ref<string>('');
const selectedRegionIdFilter = ref<number | ''>('');
const selectedLocationIdFilter = ref<number | ''>('');
const selectedTrainingCodeFilter = ref<string>('');
const startDateFilter = ref<string>('');
const endDateFilter = ref<string>('');
const selectedReportTypeFilter = ref<string>('training');

const reportQuery = computed(() => ({
  sortBy: 'userDisplayName',
  sortDir: 'asc' as const,
  userId: selectedUserIdFilter.value || undefined,
  regionId: selectedRegionIdFilter.value === '' ? undefined : selectedRegionIdFilter.value,
  locationId: selectedLocationIdFilter.value === '' ? undefined : selectedLocationIdFilter.value,
  trainingCode: selectedTrainingCodeFilter.value.trim() || undefined,
  status: statusFilter.value === 'all' ? undefined : statusFilter.value,
  startDate: startDateFilter.value || undefined,
  endDate: endDateFilter.value || undefined,
}));

const { data, error, isFetching, execute } = useUserTrainingReport(reportQuery);

const userQuery = computed(() => ({
  IsEnabled: true,
}));

const { data: usersData, execute: executeUsersQuery } = getApiUsers(userQuery, {
  options: {
    immediate: false,
  },
});

const { data: regionsData, execute: executeRegionsQuery } = getApiRegion({
  options: {
    immediate: false,
  },
});

const { data: locationsData, execute: executeLocationsQuery } = getApiLocationAll({
  options: {
    immediate: false,
  },
});

const { data: trainingLookupData, execute: executeTrainingLookup } = useTrainingLookup(false, {
  options: {
    immediate: false,
  },
});

const statusOptions: SelectOption[] = [
  { code: 'all', description: 'All statuses' },
  { code: 'active', description: 'Active' },
  { code: 'expired', description: 'Expired' },
  { code: 'notTaken', description: 'Not taken' },
];

const reportTypeOptions: SelectOption[] = [{ code: 'training', description: 'Training' }];

const regionOptions = computed<SelectOption[]>(() => {
  return buildSelectOptions(regionsData.value ?? [], 'All regions', (region) => {
    const id = region.id;
    const name = region.name?.trim() ?? '';

    return id != null && name
      ? {
          code: id,
          description: name,
        }
      : null;
  });
});

const locationOptions = computed<SelectOption[]>(() => {
  return buildSelectOptions(locationsData.value ?? [], 'All locations', (location) => {
    const id = location.id;
    const name = location.name?.trim() ?? '';

    return id != null && name
      ? {
          code: id,
          description: name,
        }
      : null;
  });
});

const userOptions = computed<SelectOption[]>(() => {
  return buildSelectOptions(usersData.value ?? [], 'All users', (user) => ({
    code: user.id,
    description: buildUserLabel(user),
  }));
});

const trainingOptions = computed<SelectOption[]>(() => {
  return buildSelectOptions(trainingLookupData.value ?? [], 'All trainings', (training) => {
    const code = training.code?.trim() ?? '';

    return {
      code,
      description: code,
    };
  });
});

const headers = [
  { title: 'User', key: 'userDisplayName', sortable: true },
  { title: 'Training Type', key: 'trainingCode', sortable: true },
  { title: 'Region', key: 'regionName', sortable: true },
  { title: 'Location', key: 'locationName', sortable: true },
  { title: 'Awarded On', key: 'awardedOn', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: true },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Version', key: 'version', sortable: true },
];

const csvColumns: ReportCsvColumn<UserTrainingReportItem>[] = [
  { header: 'User', value: (row) => row.userDisplayName },
  { header: 'Region', value: (row) => row.regionName ?? '' },
  { header: 'Location', value: (row) => row.locationName ?? '' },
  { header: 'Training Type', value: (row) => row.trainingCode },
  { header: 'Description', value: (row) => row.trainingDescription },
  { header: 'Awarded On', value: (row) => formatDateCellValue(row.awardedOn) },
  { header: 'Ending On', value: (row) => formatDateCellValue(row.endingOn) },
  { header: 'Expiry Date', value: (row) => formatDateCellValue(row.expiryDate) },
  { header: 'Status', value: (row) => row.status },
  { header: 'Version', value: (row) => row.version },
  { header: 'Notice State', value: (row) => row.noticeState },
  { header: 'Notes', value: (row) => row.notes },
];

const formattedRows = computed<Record<string, unknown>[]>(() => {
  const rows = data.value?.rows ?? [];

  return rows.map((row: UserTrainingReportItem) => ({
    ...formatRow(row),
    __isMissingMandatoryTrainingAssignment: row.hasMissingMandatoryTrainingAssignment,
  }));
});

const canExportRows = computed(() => (data.value?.rows.length ?? 0) > 0);

const getRowProps = (context: { item: Record<string, unknown> }) => {
  return context.item.__isMissingMandatoryTrainingAssignment === true
    ? { class: 'user-training-report-row--missing-mandatory' }
    : {};
};

const runReport = async () => {
  if (!canAccessUserTrainingReport.value) {
    return;
  }

  await execute();
};

const exportCsv = () => {
  if (!canAccessUserTrainingReport.value || !canExportRows.value) {
    return;
  }

  const rows = data.value?.rows ?? [];
  exportReportToCsv({
    fileName: `user_training_report_${new Date().toISOString().slice(0, 10)}.csv`,
    rows,
    columns: csvColumns,
  });
};

if (canAccessUserTrainingReport.value) {
  void executeTrainingLookup();
  void executeUsersQuery();
  void executeRegionsQuery();
  void executeLocationsQuery();
}

function buildUserLabel(user: { firstName: string; lastName: string }): string {
  const firstName = user.firstName?.trim() ?? '';
  const lastName = user.lastName?.trim() ?? '';

  return [lastName, firstName].filter(Boolean).join(', ');
}

function buildSelectOptions<TItem>(
  items: readonly TItem[],
  allLabel: string,
  mapItem: (item: TItem) => SelectOption | null,
): SelectOption[] {
  const options = items.map(mapItem).filter((option): option is SelectOption => option !== null);

  return [{ code: '', description: allLabel }, ...options];
}

function formatRow(row: UserTrainingReportItem): Record<string, unknown> {
  return {
    userDisplayName: row.userDisplayName,
    regionName: row.regionName,
    locationName: row.locationName,
    trainingCode: row.trainingCode,
    trainingDescription: row.trainingDescription,
    awardedOn: formatDateCellValue(row.awardedOn),
    endingOn: formatDateCellValue(row.endingOn),
    expiryDate: formatDateCellValue(row.expiryDate),
    status: row.status,
    version: row.version,
    noticeState: row.noticeState,
    notes: row.notes,
  };
}

function formatDateCellValue(value: unknown): unknown {
  if (typeof value !== 'string') {
    return value;
  }

  const parsed = Date.parse(value);
  return Number.isNaN(parsed) ? value : new Date(parsed).toLocaleDateString();
}
</script>

<template>
  <div v-if="!canAccessUserTrainingReport" class="user-training-report-page">
    <UaPlaceholderPage title="User Training Report" description="You do not have permission to generate reports." />
  </div>

  <div v-else class="user-training-report-page">
    <UaCard title="Filters">
      <div class="filters-grid">
        <div class="filter-field">
          <label class="filter-label" for="user-training-report-user-name">User</label>
          <UaSelect id="user-training-report-user-name" v-model="selectedUserIdFilter" :items="userOptions" label="" />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-training-code">Training Types</label>
          <UaSelect
            id="user-training-report-training-code"
            v-model="selectedTrainingCodeFilter"
            :items="trainingOptions"
          />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-region">Region</label>
          <UaSelect id="user-training-report-region" v-model="selectedRegionIdFilter" :items="regionOptions" label="" />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-location">Location</label>
          <UaSelect
            id="user-training-report-location"
            v-model="selectedLocationIdFilter"
            :items="locationOptions"
            label=""
          />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-status">Status</label>
          <UaSelect id="user-training-report-status" v-model="statusFilter" :items="statusOptions" label="" />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-start-date">Start date</label>
          <UaTextField id="user-training-report-start-date" v-model="startDateFilter" label="" type="date" clearable />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-end-date">End date</label>
          <UaTextField id="user-training-report-end-date" v-model="endDateFilter" label="" type="date" clearable />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-report-type">Report Type</label>
          <UaSelect
            id="user-training-report-report-type"
            v-model="selectedReportTypeFilter"
            :items="reportTypeOptions"
            label=""
          />
        </div>
      </div>

      <template #actions>
        <UaBtn :loading="isFetching" @click="runReport">Generate report</UaBtn>
      </template>
    </UaCard>

    <UaAlert v-if="error" type="error" :closable="false">Failed to generate report: {{ error.message }}</UaAlert>
    <div class="report-table-actions">
      <UaBtn variant="outlined" :disabled="isFetching || !canExportRows" @click="exportCsv">Export CSV</UaBtn>
    </div>
    <UaDataTable
      :headers="headers"
      :items="formattedRows"
      :row-props="getRowProps"
      :loading="isFetching"
      :paginate="false"
      searchable
      search-placeholder="Search report rows"
    />
  </div>
</template>

<style scoped>
.user-training-report-page {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
  padding: var(--ua-spacing-xl);
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(220px, 1fr));
  gap: var(--ua-spacing-md);
}

.filter-field {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.filter-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.report-table-actions {
  display: flex;
  justify-content: flex-end;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.user-training-report-row--missing-mandatory > td) {
  background:
    linear-gradient(rgba(var(--v-theme-error), 0.14), rgba(var(--v-theme-error), 0.14)),
    repeating-linear-gradient(
      -45deg,
      rgba(var(--v-theme-error), 0.06) 0,
      rgba(var(--v-theme-error), 0.06) 10px,
      rgba(var(--v-theme-error), 0.02) 10px,
      rgba(var(--v-theme-error), 0.02) 20px
    );
  border-top: 1px solid rgba(var(--v-theme-error), 0.5) !important;
  border-bottom: 1px solid rgba(var(--v-theme-error), 0.5) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.user-training-report-row--missing-mandatory > td:first-child) {
  border-left: 3px solid rgba(var(--v-theme-error), 0.8) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.user-training-report-row--missing-mandatory > td:last-child) {
  border-right: 1px solid rgba(var(--v-theme-error), 0.5) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.user-training-report-row--missing-mandatory:hover > td) {
  background-color: rgba(var(--v-theme-error), 0.18) !important;
}

@media (max-width: 1200px) {
  .filters-grid {
    grid-template-columns: repeat(2, minmax(220px, 1fr));
  }
}

@media (max-width: 768px) {
  .user-training-report-page {
    padding: var(--ua-spacing-lg);
  }

  .filters-grid {
    grid-template-columns: 1fr;
  }
}
</style>
