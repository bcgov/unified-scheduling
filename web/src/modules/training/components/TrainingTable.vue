<script setup lang="ts">
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import { mdiCheck, mdiClockOutline, mdiDragVertical, mdiPencil, mdiRestore } from '@mdi/js';
import { computed, ref } from 'vue';

type TrainingReorderPayload = {
  trainingId: number;
  newOrder: number;
};

type DataTableReorderPayload = {
  item: TrainingLookupResponse;
  newIndex: number;
  oldIndex: number;
};

const props = defineProps<{
  items: TrainingLookupResponse[];
  loading: boolean;
  canEdit?: boolean;
  disableReorder?: boolean;
  highlightExpiredRows?: boolean;
}>();

const emit = defineEmits<{
  (e: 'edit', item: TrainingLookupResponse): void;
  (e: 'expire', item: TrainingLookupResponse): void;
  (e: 'unexpire', item: TrainingLookupResponse): void;
  (e: 'reorder', payload: TrainingReorderPayload): void;
}>();

const sortBy = ref<{ key: string; order: string }[]>([]);
const searchQuery = ref('');
const isSortedByNonOrderColumn = computed(() => {
  // Allow reordering only if no sort is applied, or sorting by order column
  return sortBy.value.length > 0 && sortBy.value[0]?.key !== 'order';
});
const hasActiveSearch = computed(() => searchQuery.value.trim().length > 0);
const isDragDisabled = computed(
  () => isSortedByNonOrderColumn.value || hasActiveSearch.value || Boolean(props.disableReorder),
);
const isPaginationEnabled = computed(() => !props.canEdit);

const headers = [
  ...(props.canEdit ? [{ title: '', key: 'dragHandle', sortable: false, width: 40, align: 'center' as const }] : []),
  { title: 'Training', key: 'code', sortable: true },
  { title: 'Description', key: 'description', sortable: true },
  { title: 'Mandatory', key: 'mandatory', sortable: true, align: 'center' as const },
  { title: 'Validity (Days)', key: 'validityDays', sortable: true, align: 'end' as const },
  { title: 'Advance Notice (Days)', key: 'advanceNoticeDays', sortable: true, align: 'end' as const },
  { title: 'Rotating', key: 'rotating', sortable: true, align: 'center' as const },
  { title: 'Category', key: 'trainingCategoryName', sortable: true },
  { title: 'Status', key: 'status', sortable: false, align: 'center' as const },
  ...(props.canEdit ? [{ title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 200 }] : []),
];

const formatOptionalNumber = (value: number | null | undefined): string => {
  return typeof value === 'number' ? String(value) : '—';
};

const formatOptionalText = (value: string | null | undefined): string => {
  return value?.trim() ? value : '—';
};

const handleReorder = ({ item, newIndex }: DataTableReorderPayload) => {
  emit('reorder', { trainingId: item.id, newOrder: newIndex });
};

const isTrainingExpired = (item: TrainingLookupResponse): boolean => {
  if (!item.expiryDate) {
    return false;
  }

  return new Date(item.expiryDate).getTime() <= Date.now();
};

const getRowProps = (context: { item: TrainingLookupResponse }) => {
  const shouldHighlight = Boolean(props.highlightExpiredRows) && isTrainingExpired(context.item);

  return shouldHighlight
    ? {
        class: 'training-row--expired',
      }
    : {};
};
</script>

<template>
  <UaDataTable
    :headers="headers"
    :items="items"
    :row-props="getRowProps"
    :loading="loading"
    searchable
    v-model:search="searchQuery"
    :items-per-page="10"
    :paginate="isPaginationEnabled"
    :draggable="Boolean(canEdit) && !isDragDisabled"
    @update:sort-by="sortBy = $event"
    hover
    @reorder="handleReorder"
  >
    <template #[`item.dragHandle`]>
      <span
        v-if="canEdit"
        class="drag-handle"
        :class="{ 'drag-handle--disabled': isDragDisabled }"
        role="button"
        aria-label="Drag to reorder"
        title="Drag to reorder"
      >
        <v-icon :icon="mdiDragVertical" size="18" />
      </span>
    </template>

    <template #[`item.trainingCategoryName`]="{ item }">
      {{ formatOptionalText(item.trainingCategoryName) }}
    </template>

    <template #[`item.mandatory`]="{ item }">
      <v-icon v-if="item.mandatory" :icon="mdiCheck" color="success" size="small" />
    </template>

    <template #[`item.validityDays`]="{ item }">
      {{ formatOptionalNumber(item.validityDays) }}
    </template>

    <template #[`item.advanceNoticeDays`]="{ item }">
      {{ formatOptionalNumber(item.advanceNoticeDays) }}
    </template>

    <template #[`item.rotating`]="{ item }">
      <v-icon v-if="item.rotating" :icon="mdiCheck" color="success" size="small" />
    </template>

    <template #[`item.status`]="{ item }">
      <span :class="{ 'training-status-expired': isTrainingExpired(item) }">
        {{ isTrainingExpired(item) ? 'Expired' : 'Active' }}
      </span>
    </template>

    <template v-if="canEdit" #[`item.actions`]="{ item }">
      <div class="actions">
        <UaBtn
          icon
          variant="text"
          size="small"
          aria-label="Edit training"
          title="Edit training"
          @click="emit('edit', item)"
        >
          <v-icon :icon="mdiPencil" />
        </UaBtn>

        <UaBtn
          icon
          variant="text"
          size="small"
          :color="isTrainingExpired(item) ? 'success' : 'warning'"
          :aria-label="isTrainingExpired(item) ? 'Unexpire training' : 'Expire training'"
          :title="isTrainingExpired(item) ? 'Unexpire training' : 'Expire training'"
          @click="isTrainingExpired(item) ? emit('unexpire', item) : emit('expire', item)"
        >
          <v-icon :icon="isTrainingExpired(item) ? mdiRestore : mdiClockOutline" />
        </UaBtn>
      </div>
    </template>

    <template #no-data>
      <span class="no-data-text">No trainings found.</span>
    </template>
  </UaDataTable>
</template>

<style scoped>
.no-data-text {
  color: var(--ua-text-secondary);
}

.drag-handle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: grab;
  color: var(--ua-text-secondary);
  padding: var(--ua-spacing-sm);
  margin: calc(var(--ua-spacing-sm) * -1);
  border-radius: var(--ua-border-radius-sm);
  transition: all 0.2s ease-in-out;
}

.drag-handle:hover {
  color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.1);
}

.drag-handle--disabled {
  opacity: 0.5;
  cursor: default;
}

.drag-handle:active {
  cursor: grabbing;
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ua-spacing-xs);
}

.training-status-expired {
  color: rgb(var(--v-theme-warning));
  font-weight: var(--ua-font-weight-medium);
}

.ua-data-table-wrapper :deep(.v-table tbody tr.training-row--expired > td) {
  background:
    linear-gradient(rgba(var(--v-theme-warning), 0.12), rgba(var(--v-theme-warning), 0.12)),
    repeating-linear-gradient(
      -45deg,
      rgba(var(--v-theme-warning), 0.05) 0,
      rgba(var(--v-theme-warning), 0.05) 10px,
      rgba(var(--v-theme-warning), 0.02) 10px,
      rgba(var(--v-theme-warning), 0.02) 20px
    );
  border-top: 1px solid rgba(var(--v-theme-warning), 0.45) !important;
  border-bottom: 1px solid rgba(var(--v-theme-warning), 0.45) !important;
  color: var(--ua-text-primary);
}

.ua-data-table-wrapper :deep(.v-table tbody tr.training-row--expired > td:first-child) {
  border-left: 3px solid rgba(var(--v-theme-warning), 0.75) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.training-row--expired > td:last-child) {
  border-right: 1px solid rgba(var(--v-theme-warning), 0.45) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.training-row--expired:hover > td) {
  background-color: rgba(var(--v-theme-warning), 0.16) !important;
}

.ua-data-table-wrapper :deep(.v-table tbody tr.training-row--expired .v-icon) {
  opacity: 0.95;
}
</style>
