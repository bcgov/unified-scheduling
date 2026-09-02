<script setup lang="ts">
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

withDefaults(
  defineProps<{
    detailRows: ShiftDetailRow[];
    deleteConfirmationLabel?: string;
    deleteDisabledReason?: string;
    deleteWarning?: string;
    isDeleteConfirmed: boolean;
  }>(),
  {
    deleteConfirmationLabel: 'I understand this shift will be permanently deleted. For all assigned users.',
    deleteDisabledReason: '',
    deleteWarning: "This can't be undone.",
  },
);

const emit = defineEmits<{
  'update:isDeleteConfirmed': [value: boolean];
}>();
</script>

<template>
  <section class="shift-delete-panel" aria-label="Delete Shift Panel">
    <CalendarSchedulingShiftDetailsPanel :detail-rows="detailRows" />

    <p v-if="deleteDisabledReason" class="shift-delete-panel__warning">{{ deleteDisabledReason }}</p>
    <template v-else>
      <p class="shift-delete-panel__warning">{{ deleteWarning }}</p>
      <v-checkbox
        :model-value="isDeleteConfirmed"
        :label="deleteConfirmationLabel"
        hide-details
        @update:model-value="(value: boolean | null) => emit('update:isDeleteConfirmed', value === true)"
      />
    </template>
  </section>
</template>

<style scoped>
.shift-delete-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.shift-delete-panel__warning {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}
</style>
