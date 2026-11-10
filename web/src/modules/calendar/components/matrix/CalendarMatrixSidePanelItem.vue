<script setup lang="ts">
import { computed, inject } from 'vue';
import { mdiAlert, mdiDragVertical } from '@mdi/js';
import { DateTime } from 'luxon';
import { formatCalendarDateTimeRange, formatCalendarEventDate, formatCalendarEventTimeRange } from '@/utils/date';
import UaActionPrompt from '@/shared/components/UaActionPrompt.vue';
import { calendarMatrixContextKey } from './calendarMatrixContext';
import type {
  CalendarMatrixDragPayload,
  CalendarMatrixSidePanelItem,
  CalendarSidebarWarning,
} from './calendarMatrixTypes';

const props = defineProps<{
  item: CalendarMatrixSidePanelItem;
  timeZone?: string;
}>();

const emit = defineEmits<{
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
  (event: 'itemClick', payload: CalendarMatrixSidePanelItem): void;
}>();

const matrixContext = inject(calendarMatrixContextKey, undefined);
const warningGroups = computed(() => props.item.warningGroups?.filter((group) => group.warnings.length) ?? []);

function formatWarning(warning: CalendarSidebarWarning) {
  const timeZone = props.timeZone ?? 'UTC';
  if (!warning.allDay && warning.end) {
    return formatCalendarDateTimeRange(warning.start, warning.end, timeZone);
  }

  const options = { allDay: warning.allDay, timeZone };
  const startDate = formatCalendarEventDate(warning.start, options);
  // All-day ends are exclusive. Display the final included date for multi-day leave.
  const lastDate =
    warning.allDay && warning.end
      ? DateTime.fromISO(warning.end, { zone: timeZone }).minus({ days: 1 }).toISODate()
      : undefined;
  const dates =
    lastDate && lastDate > warning.start ? `${startDate} - ${formatCalendarEventDate(lastDate, options)}` : startDate;
  return `${dates}, ${formatCalendarEventTimeRange(warning.start, warning.end, options)}`;
}

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
    :class="{
      'is-draggable': item.draggable,
      'is-clickable': item.type === 'assignment',
      'is-hoverable': item.type === 'assignment' || item.type === 'user',
      'has-warnings': warningGroups.length > 0,
      'ua-action-prompt--highlighted': Boolean(item.prompt),
    }"
    :draggable="item.draggable === true"
    @click="emit('itemClick', item)"
    @dragstart="handleDragStart"
    @dragend="handleDragEnd"
  >
    <UaActionPrompt v-if="item.prompt" :prompt="item.prompt" parent-activator />
    <slot :item="item">
      <span v-if="item.avatarText" class="calendar-matrix-side-panel-item__avatar">{{ item.avatarText }}</span>
      <span class="calendar-matrix-side-panel-item__content">
        <span class="calendar-matrix-side-panel-item__title-row">
          <span class="calendar-matrix-side-panel-item__title">{{ item.title }}</span>
          <v-tooltip
            v-if="warningGroups.length"
            location="end"
            open-on-click
            open-on-focus
            content-class="bg-surface text-on-surface border"
          >
            <template #activator="{ props: activatorProps }">
              <button
                v-bind="activatorProps"
                type="button"
                class="calendar-matrix-side-panel-item__warning"
                :aria-label="`Event warnings for ${item.title}`"
                :draggable="false"
                @mousedown.stop
                @touchstart.stop
                @dragstart.stop.prevent
                @click.stop
                @keydown.stop
              >
                <v-icon :icon="mdiAlert" color="warning" size="14" aria-hidden="true" />
              </button>
            </template>
            <div v-for="group in warningGroups" :key="group.id">
              <strong>{{ group.heading }}</strong>
              <ul class="calendar-matrix-side-panel-item__warnings">
                <li v-for="warning in group.warnings" :key="warning.id">
                  <span>{{ warning.title }}</span>
                  <div>{{ formatWarning(warning) }}</div>
                </li>
              </ul>
            </div>
          </v-tooltip>
        </span>
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
    <v-tooltip
      v-if="item.draggable"
      text="Click and drag to a day."
      location="bottom"
      content-class="calendar-matrix-side-panel-item__tooltip"
    >
      <template #activator="{ props: tooltipProps }">
        <span
          v-bind="tooltipProps"
          class="calendar-matrix-side-panel-item__drag-icon"
          aria-label="Click and drag to a day."
        >
          <v-icon :icon="mdiDragVertical" size="18" aria-hidden="true" />
        </span>
      </template>
    </v-tooltip>
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
  position: relative;
}

.calendar-matrix-side-panel-item.has-warnings {
  padding-right: calc(var(--ua-spacing-sm) + 1.5rem);
}

.calendar-matrix-side-panel-item.has-warnings.is-draggable {
  min-height: calc(2 * var(--ua-spacing-sm) + 0.75rem + var(--ua-spacing-xs) + 1.125rem);
}

.calendar-matrix-side-panel-item__warning {
  align-items: center;
  background: transparent;
  border: 0;
  cursor: pointer;
  display: inline-flex;
  flex: 0 0 auto;
  height: 0.75rem;
  justify-content: center;
  padding: 0;
  width: 0.75rem;
}

.calendar-matrix-side-panel-item__warning:focus-visible {
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: 2px;
}

.calendar-matrix-side-panel-item__warnings {
  margin: var(--ua-spacing-xs) 0 var(--ua-spacing-sm);
  padding-left: var(--ua-spacing-md);
}

.calendar-matrix-side-panel-item.is-draggable {
  cursor: grab;
}

.calendar-matrix-side-panel-item.ua-action-prompt--highlighted {
  border: 2px solid rgb(var(--v-theme-accent));
  box-shadow: inset 0 0 0 1px rgb(var(--v-theme-primary));
}

@media (hover: hover) and (pointer: fine) {
  .calendar-matrix-side-panel-item.is-hoverable:hover {
    background: rgb(var(--v-theme-surface-variant));
  }
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
  min-width: 0;
}

.calendar-matrix-side-panel-item__drag-icon {
  align-items: center;
  color: rgb(var(--v-theme-primary));
  display: inline-flex;
  flex: 0 0 auto;
  margin-left: auto;
}

.calendar-matrix-side-panel-item.has-warnings .calendar-matrix-side-panel-item__drag-icon {
  justify-content: center;
  position: absolute;
  top: calc(var(--ua-spacing-sm) + 0.75rem + var(--ua-spacing-xs));
  right: var(--ua-spacing-sm);
  width: 0.75rem;
}

:global(.v-tooltip > .calendar-matrix-side-panel-item__tooltip.v-overlay__content) {
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
}

.calendar-matrix-side-panel-item__title-row {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-xs);
  min-width: 0;
}

.calendar-matrix-side-panel-item__title {
  min-width: 0;
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
