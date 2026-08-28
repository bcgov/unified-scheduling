<script setup lang="ts">
import { computed, ref } from 'vue';
import { Permissions } from '@/api-access/generated/models';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import type { SelectOption } from '@/types/select';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { useTrainingLookup } from '@/modules/training/trainingLookupApi';
import { useUserTrainingReport, type UserTrainingReportItem } from './userTrainingReportApi';

const accessControl = useAccessControl();
const canAccessUserTrainingReport = computed(() => accessControl.hasPermission(Permissions.ReportsGenerate));

const statusFilter = ref<'all' | 'active' | 'expired'>('all');
const selectedUserIdFilter = ref<string>('');
const selectedTrainingCodeFilter = ref<string>('');
const startDateFilter = ref<string>('');
const endDateFilter = ref<string>('');

const reportQuery = computed(() => ({
  page: 1,
  pageSize: 500,
  sortBy: 'userDisplayName',
  sortDir: 'asc' as const,
  userId: selectedUserIdFilter.value || undefined,
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

const { data: trainingLookupData, execute: executeTrainingLookup } = useTrainingLookup(false, {
  options: {
    immediate: false,
  },
});

const statusOptions: SelectOption[] = [
  { code: 'all', description: 'All statuses' },
  { code: 'active', description: 'Active only' },
  { code: 'expired', description: 'Expired only' },
];

const userOptions = computed<SelectOption[]>(() => {
  const options = (usersData.value ?? [])
    .map((user) => ({
      code: user.id,
      description: buildUserLabel(user),
    }))
    .sort((left, right) => left.description.localeCompare(right.description));

  return [{ code: '', description: 'All users' }, ...options];
});

const trainingOptions = computed<SelectOption[]>(() => {
  const options = (trainingLookupData.value ?? []).map((training) => {
    const code = training.code?.trim() ?? '';

    return {
      code,
      description: code,
    };
  });

  return [{ code: '', description: 'All trainings' }, ...options];
});

const headers = [
  { title: 'User', key: 'userDisplayName', sortable: true },
  { title: 'Training Type', key: 'trainingCode', sortable: true },
  { title: 'Description', key: 'trainingDescription', sortable: true },
  { title: 'Awarded On', key: 'awardedOn', sortable: true },
  { title: 'Ending On', key: 'endingOn', sortable: true },
  { title: 'Expiry Date', key: 'expiryDate', sortable: true },
  { title: 'Status', key: 'status', sortable: false },
  { title: 'Version', key: 'version', sortable: true },
  { title: 'Notice State', key: 'noticeState', sortable: true },
  { title: 'Notes', key: 'notes', sortable: false },
];

const formattedRows = computed<Record<string, unknown>[]>(() => {
  const rows = data.value?.rows ?? [];

  return rows.map((row: UserTrainingReportItem) => ({
    ...formatRow(row),
    __isMissingMandatoryTrainingAssignment: row.hasMissingMandatoryTrainingAssignment,
  }));
});

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

if (canAccessUserTrainingReport.value) {
  void runReport();
  void executeTrainingLookup();
  void executeUsersQuery();
}

function buildUserLabel(user: { firstName: string; lastName: string }): string {
  const firstName = user.firstName?.trim() ?? '';
  const lastName = user.lastName?.trim() ?? '';

  return [lastName, firstName].filter(Boolean).join(', ');
}

function formatRow(row: UserTrainingReportItem): Record<string, unknown> {
  return {
    userDisplayName: row.userDisplayName,
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
    <UaPageHeader title="User Training Report" />

    <UaCard title="Filters">
      <div class="filters-grid">
        <div class="filter-field">
          <label class="filter-label" for="user-training-report-user-name">User</label>
          <UaSelect id="user-training-report-user-name" v-model="selectedUserIdFilter" :items="userOptions" label="" />
        </div>

        <div class="filter-field">
          <label class="filter-label" for="user-training-report-training-code">Training</label>
          <UaSelect
            id="user-training-report-training-code"
            v-model="selectedTrainingCodeFilter"
            :items="trainingOptions"
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
      </div>

      <template #actions>
        <UaBtn :loading="isFetching" @click="runReport">Generate report</UaBtn>
      </template>
    </UaCard>

    <UaAlert v-if="error" type="error" :closable="false">Failed to generate report: {{ error.message }}</UaAlert>
    <UaDataTable
      :headers="headers"
      :items="formattedRows"
      :row-props="getRowProps"
      :loading="isFetching"
      :paginate="true"
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
