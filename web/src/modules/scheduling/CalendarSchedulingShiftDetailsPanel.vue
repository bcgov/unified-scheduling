<script setup lang="ts">
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

withDefaults(
  defineProps<{
    detailRows: ShiftDetailRow[];
    isLoading?: boolean;
    skeletonRows?: number;
  }>(),
  {
    isLoading: false,
    skeletonRows: 7,
  },
);
</script>

<template>
  <section class="shift-details-panel" aria-label="Shift Details Panel">
    <div v-if="isLoading" class="shift-details-panel__grid" aria-busy="true" aria-label="Loading Details">
      <template v-for="index in skeletonRows" :key="index">
        <v-skeleton-loader class="shift-details-panel__skeleton shift-details-panel__skeleton--label" type="text" />
        <v-skeleton-loader class="shift-details-panel__skeleton shift-details-panel__skeleton--value" type="text" />
      </template>
    </div>

    <div v-else class="shift-details-panel__grid">
      <template v-for="detail in detailRows" :key="detail.label">
        <span class="shift-details-panel__label">{{ detail.label }}</span>
        <div class="shift-details-panel__value">
          <RRuleEditor
            v-if="'recurrenceRule' in detail"
            :model-value="detail.recurrenceRule"
            :start-date="detail.recurrenceStartDate"
            read-only
          />
          <span
            v-else-if="detail.color"
            class="shift-details-panel__color-sphere"
            :style="{ backgroundColor: detail.color }"
            :aria-label="detail.value"
            :title="detail.value"
            role="img"
          />
          <template v-else>{{ detail.value }}</template>
        </div>
      </template>
    </div>
  </section>
</template>

<style scoped>
.shift-details-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.shift-details-panel__grid {
  align-items: baseline;
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: 150px minmax(0, 1fr);
}

.shift-details-panel__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  line-height: 1.4;
}

.shift-details-panel__value {
  align-items: center;
  color: var(--ua-text-primary);
  display: flex;
  font-size: var(--ua-font-size-sm);
  line-height: 1.4;
  margin: 0;
  overflow-wrap: anywhere;
  white-space: pre-line;
}

.shift-details-panel__color-sphere {
  border: 1px solid rgb(var(--v-theme-outline));
  border-radius: 999px;
  display: inline-block;
  height: 1.25rem;
  width: 1.25rem;
}

.shift-details-panel__skeleton {
  background: transparent;
}

.shift-details-panel__skeleton--label {
  max-width: 140px;
}

.shift-details-panel__skeleton--value {
  max-width: 100%;
}

.shift-details-panel__skeleton--value:nth-of-type(4n) {
  max-width: 65%;
}

:deep(.shift-details-panel__skeleton .v-skeleton-loader__text) {
  margin: 0;
}

.shift-details-panel__skeleton--label :deep(.v-skeleton-loader__text) {
  height: 1.25rem;
}

.shift-details-panel__skeleton--value :deep(.v-skeleton-loader__text) {
  height: 1rem;
}

@media (max-width: 640px) {
  .shift-details-panel__grid {
    gap: var(--ua-spacing-xs) 0;
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
