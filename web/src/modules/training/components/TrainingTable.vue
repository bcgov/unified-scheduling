<script setup lang="ts">
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { mdiCheck, mdiDragVertical, mdiPencil } from '@mdi/js';

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
}>();

const emit = defineEmits<{
  (e: 'edit', item: TrainingLookupResponse): void;
  (e: 'reorder', payload: TrainingReorderPayload): void;
}>();

const headers = [
  ...(props.canEdit ? [{ title: '', key: 'dragHandle', sortable: false, width: 40, align: 'center' as const }] : []),
  { title: 'Training', key: 'code', sortable: true },
  { title: 'Description', key: 'description', sortable: true },
  { title: 'Mandatory', key: 'mandatory', sortable: true, align: 'center' as const },
  { title: 'Validity (Days)', key: 'validityDays', sortable: true, align: 'end' as const },
  { title: 'Advance Notice(Days)', key: 'advanceNoticeDays', sortable: true, align: 'end' as const },
  { title: 'Rotating', key: 'rotating', sortable: true, align: 'center' as const },
  { title: 'Category', key: 'trainingCategoryName', sortable: true },
  ...(props.canEdit ? [{ title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 96 }] : []),
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
</script>

<template>
  <UaDataTable
    v-if="items.length > 0 || loading"
    :headers="headers"
    :items="items"
    :loading="loading"
    :items-per-page="10"
    :draggable="Boolean(canEdit)"
    hover
    @reorder="handleReorder"
  >
    <template #[`item.dragHandle`]>
      <span v-if="canEdit" class="drag-handle" role="button" aria-label="Drag to reorder" title="Drag to reorder">
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

    <template v-if="canEdit" #[`item.actions`]="{ item }">
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
    </template>

    <template #no-data>
      <span class="no-data-text">No trainings found.</span>
    </template>
  </UaDataTable>

  <UaPlaceholderPage v-else title="No trainings" description="There are no training types to display yet." />
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

.drag-handle:active {
  cursor: grabbing;
}
</style>
