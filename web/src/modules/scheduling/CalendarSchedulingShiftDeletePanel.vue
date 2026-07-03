<script setup lang="ts">
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
  deleteDisabledReason?: string;
  isDeleteConfirmed: boolean;
}>();

const emit = defineEmits<{
  'update:isDeleteConfirmed': [value: boolean];
}>();
</script>

<template>
  <section class="shift-delete-panel" aria-label="Delete shift panel">
    <CalendarSchedulingShiftDetailsPanel :detail-rows="detailRows" />

    <p v-if="deleteDisabledReason" class="shift-delete-panel__warning">{{ deleteDisabledReason }}</p>
    <template v-else>
      <p class="shift-delete-panel__warning">This can't be undone.</p>
      <v-checkbox
        :model-value="isDeleteConfirmed"
        label="I understand this shift will be permanently deleted."
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
