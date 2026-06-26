<script setup lang="ts">
import { inject } from 'vue';
import { calendarMatrixContextKey } from './calendarMatrixContext';
import type { CalendarMatrixDragPayload, CalendarMatrixSidePanelItem } from './calendarMatrixTypes';

const props = defineProps<{
  item: CalendarMatrixSidePanelItem;
}>();

const emit = defineEmits<{
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
}>();

const matrixContext = inject(calendarMatrixContextKey, undefined);

function createDragPayload(): CalendarMatrixDragPayload {
  return {
    source: 'side-panel',
    itemId: props.item.id,
    itemType: props.item.type,
    payload: props.item.payload,
  };
}

function writeDragPayload(event: DragEvent, payload: CalendarMatrixDragPayload) {
  const transferPayload = {
    source: payload.source,
    itemId: payload.itemId,
    itemType: payload.itemType,
    payload: payload.payload,
  };

  try {
    event.dataTransfer?.setData('application/json', JSON.stringify(transferPayload));
  } catch {
    event.dataTransfer?.setData(
      'application/json',
      JSON.stringify({
        source: payload.source,
        itemId: payload.itemId,
        itemType: payload.itemType,
      }),
    );
  }

  event.dataTransfer?.setData('text/plain', payload.itemId);
}

function handleDragStart(event: DragEvent) {
  if (!props.item.draggable) {
    return;
  }

  const payload = createDragPayload();
  matrixContext?.startDrag(payload);
  writeDragPayload(event, payload);
  event.dataTransfer?.setDragImage(event.currentTarget as Element, 8, 8);
  emit('dragStart', payload);
}

function handleDragEnd() {
  matrixContext?.clearDrag();
}
</script>

<template>
  <article
    class="calendar-matrix-side-panel-item"
    role="listitem"
    :aria-label="item.title"
    :class="{ 'is-draggable': item.draggable }"
    :draggable="item.draggable === true"
    @dragstart="handleDragStart"
    @dragend="handleDragEnd"
  >
    <slot :item="item">
      <span v-if="item.avatarText" class="calendar-matrix-side-panel-item__avatar">{{ item.avatarText }}</span>
      <span class="calendar-matrix-side-panel-item__content">
        <span class="calendar-matrix-side-panel-item__title">{{ item.title }}</span>
        <span v-if="item.subtitle" class="calendar-matrix-side-panel-item__subtitle">{{ item.subtitle }}</span>
        <span v-if="item.meta?.length" class="calendar-matrix-side-panel-item__meta-list">
          <span
            v-for="(metaItem, index) in item.meta"
            :key="`${metaItem.label ?? 'value'}-${metaItem.value}-${index}`"
            class="calendar-matrix-side-panel-item__meta"
          >
            <span v-if="metaItem.label" class="calendar-matrix-side-panel-item__meta-label">
              {{ metaItem.label }}:
            </span>
            {{ metaItem.value }}
          </span>
        </span>
      </span>
    </slot>
  </article>
</template>

<style scoped>
.calendar-matrix-side-panel-item {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  color: var(--ua-text-primary);
  display: flex;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-sm);
}

.calendar-matrix-side-panel-item.is-draggable {
  cursor: grab;
}

.calendar-matrix-side-panel-item__avatar {
  align-items: center;
  color: rgb(var(--v-theme-secondary));
  display: inline-flex;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 2rem;
  width: 2rem;
  justify-content: center;
}

.calendar-matrix-side-panel-item__content {
  display: grid;
  gap: 0.125rem;
}

.calendar-matrix-side-panel-item__title {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  overflow-wrap: anywhere;
}

.calendar-matrix-side-panel-item__subtitle,
.calendar-matrix-side-panel-item__meta {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  overflow-wrap: anywhere;
}

.calendar-matrix-side-panel-item__meta-list {
  display: grid;
}

.calendar-matrix-side-panel-item__meta-label {
  font-weight: var(--ua-font-weight-semibold);
}
</style>
