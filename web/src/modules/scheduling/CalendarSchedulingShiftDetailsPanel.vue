<script setup lang="ts">
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
}>();
</script>

<template>
  <section class="shift-details-panel" aria-label="Shift details panel">
    <dl class="shift-details-panel__details">
      <template v-for="detail in detailRows" :key="detail.label">
        <dt>{{ detail.label }}</dt>
        <dd>
          <RRuleEditor
            v-if="'recurrenceRule' in detail"
            :model-value="detail.recurrenceRule"
            :start-date="detail.recurrenceStartDate"
            read-only
          />
          <template v-else>{{ detail.value }}</template>
        </dd>
      </template>
    </dl>
  </section>
</template>

<style scoped>
.shift-details-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.shift-details-panel__details {
  display: grid;
  gap: var(--ua-spacing-sm) var(--ua-spacing-lg);
  grid-template-columns: minmax(120px, max-content) minmax(0, 1fr);
  margin: 0;
}

.shift-details-panel__details dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.shift-details-panel__details dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

@media (max-width: 640px) {
  .shift-details-panel__details {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
