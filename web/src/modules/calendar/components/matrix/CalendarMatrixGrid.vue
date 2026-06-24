<script setup lang="ts">
import { computed } from 'vue';
import type { CalendarEventBase } from '../../calendarTypes';
import CalendarMatrixCell from './CalendarMatrixCell.vue';
import CalendarMatrixHeader from './CalendarMatrixHeader.vue';
import CalendarMatrixResourceRow from './CalendarMatrixResourceRow.vue';
import type {
  CalendarMatrixCellDropEvent,
  CalendarMatrixCellHeaderActionEvent,
  CalendarMatrixCellHeaderClickEvent,
  CalendarMatrixDragPayload,
  CalendarMatrixEventBlockActionEvent,
  CalendarMatrixResource,
  CalendarMatrixViewModel,
} from './calendarMatrixTypes';

const props = defineProps<{
  model: CalendarMatrixViewModel;
}>();

const matrixResourceColumn =
  'minmax(var(--calendar-matrix-resource-column-min-width), var(--calendar-matrix-resource-column-max-width))';
const matrixDayColumn = 'minmax(var(--calendar-matrix-day-column-min-width), 1fr)';

const emit = defineEmits<{
  (event: 'cellDrop', payload: CalendarMatrixCellDropEvent): void;
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
  (event: 'eventAction', payload: CalendarMatrixEventBlockActionEvent): void;
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'headerAction', payload: CalendarMatrixCellHeaderActionEvent): void;
  (event: 'headerClick', payload: CalendarMatrixCellHeaderClickEvent): void;
  (event: 'resourceAdd', payload: CalendarMatrixResource): void;
}>();

const columnTemplate = computed(() => {
  return `${matrixResourceColumn} repeat(${props.model.days.length}, ${matrixDayColumn})`;
});

const cellsByKey = computed(() => {
  const lookup = new Map<string, CalendarMatrixViewModel['cells'][number]>();

  for (const cell of props.model.cells) {
    lookup.set(createCellKey(cell.resourceId, cell.date), cell);
  }

  return lookup;
});

function createCellKey(resourceId: string, date: string) {
  return `${resourceId}::${date}`;
}

function resolveCell(resourceId: string, date: string) {
  return (
    cellsByKey.value.get(createCellKey(resourceId, date)) ?? {
      resourceId,
      date,
      groups: [],
    }
  );
}
</script>

<template>
  <div class="calendar-matrix-grid">
    <div class="calendar-matrix-grid__scroller">
      <div
        class="calendar-matrix-grid__table"
        role="grid"
        :aria-label="`${model.primaryColumn.label} calendar matrix`"
        :style="{ gridTemplateColumns: columnTemplate }"
      >
        <CalendarMatrixHeader :primary-column-label="model.primaryColumn.label" :days="model.days" />

        <template v-if="model.primaryColumn.resources.length && model.days.length">
          <template v-for="resource in model.primaryColumn.resources" :key="resource.id">
            <div class="calendar-matrix-grid__row" role="row">
              <CalendarMatrixResourceRow :resource="resource" @add-resource="emit('resourceAdd', $event)">
                <template v-if="$slots['resource-row']" #default="slotProps">
                  <slot name="resource-row" v-bind="slotProps" />
                </template>
                <template v-if="$slots['resource-action']" #action="slotProps">
                  <slot name="resource-action" v-bind="slotProps" />
                </template>
              </CalendarMatrixResourceRow>
              <CalendarMatrixCell
                v-for="day in model.days"
                :key="`${resource.id}-${day.date}`"
                :cell="resolveCell(resource.id, day.date)"
                :is-today="day.isToday"
                :resource="resource"
                :time-zone="model.timeZone"
                @cell-drop="emit('cellDrop', $event)"
                @drag-start="emit('dragStart', $event)"
                @event-action="emit('eventAction', $event)"
                @event-click="emit('eventClick', $event)"
                @header-action="emit('headerAction', $event)"
                @header-click="emit('headerClick', $event)"
              >
                <template v-if="$slots.cell" #cell="slotProps">
                  <slot name="cell" v-bind="slotProps" />
                </template>
                <template v-if="$slots['event-block']" #event-block="slotProps">
                  <slot name="event-block" v-bind="slotProps" />
                </template>
                <template v-if="$slots['cell-header']" #cell-header="slotProps">
                  <slot name="cell-header" v-bind="slotProps" />
                </template>
              </CalendarMatrixCell>
            </div>
          </template>
        </template>

        <div v-else class="calendar-matrix-grid__empty">No matrix data to display.</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.calendar-matrix-grid {
  --calendar-matrix-resource-column-min-width: 13.75rem;
  --calendar-matrix-resource-column-max-width: 16.25rem;
  --calendar-matrix-day-column-min-width: 10rem;
  border: 1px solid var(--ua-border-color);
  min-width: 0;
}

.calendar-matrix-grid__scroller {
  overflow: auto;
}

.calendar-matrix-grid__table {
  background: var(--ua-border-color);
  display: grid;
  min-width: min-content;
}

.calendar-matrix-grid__row {
  display: contents;
}

:deep(.calendar-matrix-resource-row) {
  border-inline-end: 1px solid var(--ua-border-color);
  border-top: 1px solid var(--ua-border-color);
  position: sticky;
  left: 0;
  z-index: 2;
}

.calendar-matrix-grid__empty {
  background: rgb(var(--v-theme-surface));
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  grid-column: 1 / -1;
  padding: var(--ua-spacing-lg);
}
</style>
