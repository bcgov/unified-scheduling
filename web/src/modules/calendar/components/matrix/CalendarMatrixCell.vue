<script setup lang="ts">
import { computed, inject, ref } from 'vue';
import { calendarMatrixContextKey } from './calendarMatrixContext';
import CalendarMatrixEventBlock from './CalendarMatrixEventBlock.vue';
import type {
  CalendarMatrixCell,
  CalendarMatrixCellHeader as CalendarMatrixCellHeaderModel,
  CalendarMatrixCellHeaderActionEvent,
  CalendarMatrixCellDropContext,
  CalendarMatrixCellDropEvent,
  CalendarMatrixCellHeaderClickEvent,
  CalendarMatrixEventDisplay,
  CalendarMatrixEventBlockActionEvent,
  CalendarMatrixEventItem,
  CalendarMatrixDragPayload,
  CalendarMatrixResource,
} from './calendarMatrixTypes';
import type { CalendarEventBase } from '../../calendarTypes';
import CalendarMatrixCellHeader from './CalendarMatrixCellHeader.vue';

const props = defineProps<{
  cell: CalendarMatrixCell;
  resource?: CalendarMatrixResource;
  isToday?: boolean;
  timeZone?: string;
}>();

const emit = defineEmits<{
  (event: 'cellDrop', payload: CalendarMatrixCellDropEvent): void;
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
  (event: 'eventAction', payload: CalendarMatrixEventBlockActionEvent): void;
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'headerAction', payload: CalendarMatrixCellHeaderActionEvent): void;
  (event: 'headerClick', payload: CalendarMatrixCellHeaderClickEvent): void;
}>();

const matrixContext = inject(calendarMatrixContextKey, undefined);
const isDropTarget = ref(false);

const headers = computed(() => props.cell.headers ?? []);
const hasContent = computed(
  () => headers.value.length > 0 || props.cell.groups.some((group) => group.events.length > 0),
);
const isDropActive = computed(() => !!matrixContext?.activeDragPayload.value);
const cellAriaLabel = computed(() => {
  const resourceTitle = props.resource?.title ?? props.cell.resourceId;
  const eventCount = props.cell.groups.reduce((total, group) => total + group.events.length, 0);
  const headerCount = headers.value.length;
  const summary = [
    headerCount ? `${headerCount} header${headerCount === 1 ? '' : 's'}` : undefined,
    `${eventCount} event${eventCount === 1 ? '' : 's'}`,
  ]
    .filter(Boolean)
    .join(', ');

  return `${resourceTitle} on ${props.cell.date}, ${summary}`;
});

function resolveEventDisplay(
  eventItem: CalendarMatrixEventItem,
  group: CalendarMatrixCell['groups'][number],
): CalendarMatrixEventDisplay {
  return {
    color: eventItem.display?.color ?? group.color,
    status: eventItem.display?.status,
    draggable: eventItem.display?.draggable,
    action: eventItem.display?.action ?? group.action,
  };
}

function readDragPayload(event: DragEvent) {
  if (matrixContext?.activeDragPayload.value) {
    return matrixContext.activeDragPayload.value;
  }

  const jsonPayload = event.dataTransfer?.getData('application/json');

  if (!jsonPayload) {
    return undefined;
  }

  try {
    return JSON.parse(jsonPayload) as CalendarMatrixDragPayload;
  } catch {
    return undefined;
  }
}

function canAcceptDrag(event: DragEvent) {
  return !!matrixContext?.activeDragPayload.value || event.dataTransfer?.types.includes('application/json') === true;
}

function handleDragEnter(event: DragEvent) {
  if (!canAcceptDrag(event)) {
    return;
  }

  event.preventDefault();
  isDropTarget.value = true;
}

function handleDragOver(event: DragEvent) {
  if (canAcceptDrag(event)) {
    event.preventDefault();
    isDropTarget.value = true;

    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }
}

function handleDragLeave(event: DragEvent) {
  const currentTarget = event.currentTarget as Node | null;
  const nextTarget = event.relatedTarget;

  if (currentTarget && nextTarget instanceof Node && currentTarget.contains(nextTarget)) {
    return;
  }

  isDropTarget.value = false;
}

function handleDrop(event: DragEvent) {
  const payload = readDragPayload(event);

  if (!payload) {
    isDropTarget.value = false;
    return;
  }

  event.preventDefault();
  isDropTarget.value = false;

  const dropContext: CalendarMatrixCellDropContext = {
    resourceId: props.cell.resourceId,
    resourceType: props.resource?.type,
    date: props.cell.date,
  };

  matrixContext?.dropOnCell(dropContext);
  emit('cellDrop', {
    drag: payload,
    drop: dropContext,
  });
}

function handleEventAction(payload: CalendarMatrixEventBlockActionEvent) {
  emit('eventAction', payload);
}

function handleEventClick(event: CalendarEventBase) {
  emit('eventClick', event);
}

function handleHeaderClick(header: CalendarMatrixCellHeaderModel) {
  emit('headerClick', { cell: props.cell, header });
}

function handleHeaderAction(payload: CalendarMatrixCellHeaderActionEvent) {
  emit('headerAction', payload);
}

function handleDragStart(payload: CalendarMatrixDragPayload) {
  emit('dragStart', payload);
}
</script>

<template>
  <section
    class="calendar-matrix-cell"
    role="gridcell"
    :aria-label="cellAriaLabel"
    :class="{
      'is-empty': !hasContent,
      'is-drop-active': isDropActive,
      'is-drop-target': isDropTarget,
      'is-today': isToday,
    }"
    @dragenter="handleDragEnter"
    @dragleave="handleDragLeave"
    @dragover="handleDragOver"
    @drop="handleDrop"
  >
    <slot name="cell" :cell="cell" :resource="resource" :is-today="isToday">
      <template v-for="header in headers" :key="header.id ?? `${header.text}-${header.title ?? ''}`">
        <slot
          name="cell-header"
          :cell="cell"
          :header="header"
          :on-header-action="handleHeaderAction"
          :on-header-click="handleHeaderClick"
        >
          <CalendarMatrixCellHeader
            :cell="cell"
            :header="header"
            @action="handleHeaderAction"
            @click="handleHeaderClick"
          />
        </slot>
      </template>
      <template v-for="group in cell.groups" :key="group.id">
        <div v-if="group.events.length" class="calendar-matrix-cell__group">
          <template v-for="eventItem in group.events" :key="eventItem.event.id">
            <slot
              name="event-block"
              :event="eventItem.event"
              :display="resolveEventDisplay(eventItem, group)"
              :group="group"
              :on-event-action="handleEventAction"
              :on-event-click="handleEventClick"
              :on-drag-start="handleDragStart"
            >
              <CalendarMatrixEventBlock
                :event="eventItem.event"
                :display="resolveEventDisplay(eventItem, group)"
                :variant="group.variant"
                :show-color-bar="group.showColorBar"
                :time-zone="timeZone"
                @event-action="handleEventAction"
                @drag-start="handleDragStart"
                @event-click="handleEventClick"
              />
            </slot>
          </template>
        </div>
      </template>
    </slot>
  </section>
</template>

<style scoped>
.calendar-matrix-cell {
  background: rgb(var(--v-theme-surface));
  border-inline-start: 1px solid var(--ua-border-color);
  border-top: 1px solid var(--ua-border-color);
  color: var(--ua-text-primary);
  align-content: start;
  align-items: start;
  display: grid;
  gap: var(--ua-spacing-sm);
  grid-auto-rows: max-content;
  padding: var(--ua-spacing-sm);
}

.calendar-matrix-cell.is-today {
  background:
    linear-gradient(var(--calendar-today-bg-color), var(--calendar-today-bg-color)) padding-box,
    rgb(var(--v-theme-surface)) border-box;
}

.calendar-matrix-cell.is-drop-active {
  outline: 2px groove rgba(var(--v-theme-on-surface), 0.4);
  outline-offset: -4px;
}

.calendar-matrix-cell.is-drop-target {
  background-color: rgb(var(--v-theme-surface-variant));
  outline: 2px groove rgba(var(--v-theme-primary));
  outline-offset: -4px;
}

.calendar-matrix-cell__group {
  align-content: start;
  align-items: start;
  display: grid;
  gap: var(--ua-spacing-xs);
  grid-auto-rows: max-content;
}
</style>
