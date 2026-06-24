<script setup lang="ts">
import UaBtn from '@/shared/components/UaBtn.vue';
import CalendarMatrixSidePanelItem from './CalendarMatrixSidePanelItem.vue';
import type { CalendarMatrixDragPayload, CalendarMatrixSidePanel } from './calendarMatrixTypes';

defineProps<{
  panel: CalendarMatrixSidePanel;
}>();

const emit = defineEmits<{
  (event: 'action'): void;
  (event: 'itemDragStart', payload: CalendarMatrixDragPayload): void;
}>();
</script>

<template>
  <aside class="calendar-matrix-side-panel" :aria-label="panel.label">
    <header class="calendar-matrix-side-panel__header">
      <h2 class="calendar-matrix-side-panel__title">{{ panel.label }}</h2>
      <UaBtn
        v-if="panel.actionLabel"
        variant="outlined"
        size="small"
        :aria-label="panel.actionLabel"
        @click="emit('action')"
      >
        {{ panel.actionLabel }}
      </UaBtn>
    </header>

    <div v-if="panel.items.length" class="calendar-matrix-side-panel__list" role="list">
      <CalendarMatrixSidePanelItem
        v-for="item in panel.items"
        :key="item.id"
        :item="item"
        @drag-start="emit('itemDragStart', $event)"
      >
        <template v-if="$slots.item" #default="slotProps">
          <slot name="item" v-bind="slotProps" />
        </template>
      </CalendarMatrixSidePanelItem>
    </div>
    <div v-else class="calendar-matrix-side-panel__empty">No items to display.</div>
  </aside>
</template>

<style scoped>
.calendar-matrix-side-panel {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  color: var(--ua-text-primary);
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-rows: auto minmax(0, 1fr);
  padding: var(--ua-spacing-md);
}

.calendar-matrix-side-panel__header {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: space-between;
}

.calendar-matrix-side-panel__title {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  margin: 0;
  text-transform: uppercase;
}

.calendar-matrix-side-panel__list {
  align-content: start;
  display: grid;
  gap: var(--ua-spacing-sm);
  overflow: auto;
}

.calendar-matrix-side-panel__empty {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}
</style>
