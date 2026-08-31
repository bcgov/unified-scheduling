<script setup lang="ts">
import { onMounted } from 'vue';
import { mdiArrowDown, mdiArrowUp } from '@mdi/js';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import type { AuditRecordResponseDto } from '@/api-access/generated/models';
import { AUDIT_ACTION_COLORS, type AuditAction } from '../constants';
import { buildDiffRows, formatOccurredOn } from '../utils/auditFormat';
import { useAuditHistory } from '../composables/useAuditHistory';
import AuditFilters from '../components/AuditFilters.vue';

const {
  canViewAudit,
  entityTypeOptions,
  changedFieldOptions,
  fieldLabelByName,
  isLoadingEntityTypes,
  isLoadingFields,
  entityType,
  entityPk,
  changedFields,
  actorUserId,
  actorOptions,
  isLoadingActors,
  searchActors,
  action,
  fromDate,
  toDate,
  sortDirection,
  canApply,
  page,
  pageSize,
  totalCount,
  records,
  expanded,
  isLoadingRecords,
  error,
  hasSearched,
  loadEntityTypes,
  applyFilters,
  clearFilters,
  goToPage,
  updatePageSize,
  toggleSortDirection,
} = useAuditHistory();

const ITEMS_PER_PAGE_OPTIONS = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
];

const headers = [
  { title: 'Date & Time', key: 'occurredOn', sortable: false },
  { title: 'Actor', key: 'actorName', sortable: false },
  { title: 'Action', key: 'action', sortable: false },
  { title: 'Entity Type', key: 'entityType', sortable: false },
  { title: 'Entity ID', key: 'entityPK', sortable: false },
  { title: 'Changed Fields', key: 'changedColumns', sortable: false },
  { title: '', key: 'data-table-expand', sortable: false },
];

onMounted(async () => {
  if (!canViewAudit.value) return;
  await loadEntityTypes();
});
</script>

<template>
  <div v-if="!canViewAudit" class="no-access">
    <p>You do not have permission to view this page.</p>
  </div>

  <div v-else class="audit-view">
    <UaPageHeader title="Audit Log" />

    <UaAlert v-if="error" type="error">{{ error }}</UaAlert>

    <div class="audit-layout">
      <div class="panel">
        <UaDataTable
          :headers="headers"
          :items="records"
          :loading="isLoadingRecords"
          :items-length="totalCount"
          :page="page"
          :items-per-page="pageSize"
          :items-per-page-options="ITEMS_PER_PAGE_OPTIONS"
          v-model:expanded="expanded"
          item-value="id"
          show-expand
          @update:page="goToPage"
          @update:items-per-page="updatePageSize"
        >
          <template #[`header.occurredOn`]="{ column }">
            <button type="button" class="sort-header-btn" @click="toggleSortDirection">
              {{ column.title }}
              <v-icon :icon="sortDirection === 'desc' ? mdiArrowDown : mdiArrowUp" size="16" />
            </button>
          </template>

          <template #[`item.occurredOn`]="{ item }">
            {{ formatOccurredOn(item.occurredOn) }}
          </template>

          <template #[`item.actorName`]="{ item }">
            {{ item.actorName ?? 'System' }}
          </template>

          <template #[`item.action`]="{ item }">
            <v-chip :color="AUDIT_ACTION_COLORS[item.action as AuditAction]" size="small" variant="tonal">
              {{ item.action }}
            </v-chip>
          </template>

          <template #[`item.changedColumns`]="{ item }">
            <div v-if="item.action === 'Modified' && item.changedColumns?.length" class="changed-fields">
              <v-chip v-for="field in item.changedColumns" :key="field" size="x-small" variant="tonal">
                {{ fieldLabelByName.get(field) ?? field }}
              </v-chip>
            </div>
            <span v-else>—</span>
          </template>

          <template #expanded-row="{ item, columns }">
            <tr>
              <td :colspan="columns.length" class="diff-cell">
                <table class="diff-table">
                  <thead>
                    <tr>
                      <th>Field</th>
                      <th>Before</th>
                      <th>After</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="row in buildDiffRows(item as AuditRecordResponseDto, fieldLabelByName)" :key="row.field">
                      <td>{{ row.label }}</td>
                      <td>{{ row.before }}</td>
                      <td>{{ row.after }}</td>
                    </tr>
                  </tbody>
                </table>
              </td>
            </tr>
          </template>

          <template #no-data>
            <span class="no-data-text">
              {{
                hasSearched
                  ? 'No audit records found.'
                  : 'Select an entity type and click Search to view audit history.'
              }}
            </span>
          </template>
        </UaDataTable>
      </div>

      <AuditFilters
        v-model:entity-type="entityType"
        v-model:entity-pk="entityPk"
        v-model:changed-fields="changedFields"
        v-model:actor-user-id="actorUserId"
        v-model:action="action"
        v-model:from-date="fromDate"
        v-model:to-date="toDate"
        :entity-type-options="entityTypeOptions"
        :changed-field-options="changedFieldOptions"
        :actor-options="actorOptions"
        :is-loading-entity-types="isLoadingEntityTypes"
        :is-loading-fields="isLoadingFields"
        :is-loading-actors="isLoadingActors"
        :loading="isLoadingRecords"
        :can-apply="canApply"
        @apply="applyFilters"
        @clear="clearFilters"
        @search:actor="searchActors"
      />
    </div>
  </div>
</template>

<style scoped>
.audit-view {
  padding: var(--ua-spacing-xl);
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
}

.audit-layout {
  display: grid;
  grid-template-columns: 1fr 280px;
  gap: var(--ua-spacing-lg);
  align-items: start;
}

.panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
}

.sort-header-btn {
  display: inline-flex;
  align-items: center;
  gap: var(--ua-spacing-xs);
  background: none;
  border: none;
  padding: 0;
  font: inherit;
  font-weight: var(--ua-font-weight-bold);
  color: inherit;
  cursor: pointer;
}

.changed-fields {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-xs);
}

.diff-cell {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  padding: var(--ua-spacing-md) var(--ua-spacing-lg);
}

.diff-table {
  width: 100%;
  border-collapse: collapse;
}

.diff-table th {
  text-align: left;
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
  padding: var(--ua-spacing-xs) var(--ua-spacing-sm);
}

.diff-table td {
  padding: var(--ua-spacing-xs) var(--ua-spacing-sm);
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
