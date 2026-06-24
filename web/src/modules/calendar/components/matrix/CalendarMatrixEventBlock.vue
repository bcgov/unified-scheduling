<script setup lang="ts">
import { computed, inject } from 'vue';
import type { CalendarEventBase } from '../../calendarTypes';
import { formatCalendarEventTimeRange } from '@/utils/date';
import { calendarMatrixContextKey } from './calendarMatrixContext';
import { resolveMatrixStatusClass, sanitizeMatrixClassToken, toRgba } from './calendarMatrixDisplayUtils';
import { CalendarMatrixActionType } from './calendarMatrixTypes';
import type {
  CalendarMatrixEventBlockActionEvent,
  CalendarMatrixEventDisplay,
  CalendarMatrixDragPayload,
  CalendarMatrixEventGroupVariant,
} from './calendarMatrixTypes';

const props = withDefaults(
  defineProps<{
    event: CalendarEventBase;
    display?: CalendarMatrixEventDisplay;
    variant?: CalendarMatrixEventGroupVariant;
    showColorBar?: boolean;
    timeZone?: string;
  }>(),
  {
    variant: 'primary',
    showColorBar: false,
  },
);

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'eventAction', payload: CalendarMatrixEventBlockActionEvent): void;
  (event: 'dragStart', payload: CalendarMatrixDragPayload): void;
}>();

defineSlots<{
  default?: (props: {
    event: CalendarEventBase;
    display: CalendarMatrixEventDisplay | undefined;
    timeDisplay: string;
  }) => unknown;
  header?: (props: {
    event: CalendarEventBase;
    display: CalendarMatrixEventDisplay | undefined;
    timeDisplay: string;
  }) => unknown;
}>();

const matrixContext = inject(calendarMatrixContextKey, undefined);

const activeColorBackgroundAlpha = 0.35;
const allowedVariants = new Set<CalendarMatrixEventGroupVariant>(['primary', 'secondary', 'muted', 'warning']);

const statusClass = computed(() => resolveMatrixStatusClass(props.display?.status));

const variantClass = computed(() => {
  const normalizedVariant = sanitizeMatrixClassToken(props.variant, 'primary') as CalendarMatrixEventGroupVariant;
  return allowedVariants.has(normalizedVariant) ? normalizedVariant : 'primary';
});

const isSelected = computed(() => matrixContext?.selectedEventId.value === props.event.id);
const isDraggable = computed(() => props.display?.draggable === true);
const hasActionDisplay = computed(() => !!props.display?.action?.icon || !!props.display?.action?.text);
const isActionClickable = computed(
  () => hasActionDisplay.value && props.display?.action?.type !== CalendarMatrixActionType.Custom,
);
const hasColorBar = computed(() => props.showColorBar && !!props.display?.color);
const eventBlockStyle = computed(() => {
  if (!props.display?.color) {
    return undefined;
  }

  const style: Record<string, string> = {
    '--calendar-event-border-color': props.display.color,
  };

  const activeColorBackground = toRgba(props.display.color, activeColorBackgroundAlpha);

  if (hasColorBar.value && statusClass.value === 'active' && activeColorBackground) {
    style['--calendar-event-bg'] = activeColorBackground;
  }

  return style;
});

const timeDisplay = computed(() =>
  formatCalendarEventTimeRange(props.event.start, props.event.end, {
    allDay: props.event.allDay,
    timeZone: props.event.timeZoneId ?? props.timeZone,
  }),
);

function handleClick() {
  matrixContext?.selectEvent(props.event.id);
  emit('eventClick', props.event);
}

function handleActionClick() {
  const action = props.display?.action;

  if (!isActionClickable.value || !action) {
    return;
  }

  emit('eventAction', {
    event: props.event,
    actionId: action.actionId,
    actionType: action.type,
  });
}

function createDragPayload(): CalendarMatrixDragPayload {
  return {
    source: 'event-block',
    itemId: props.event.id,
    itemType: props.event.type,
    payload: props.event,
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
  if (!isDraggable.value) {
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
    class="calendar-matrix-event-block"
    :class="[
      `is-${statusClass}`,
      `has-${variantClass}-variant`,
      {
        'is-selected': isSelected,
        'is-draggable': isDraggable,
        'has-color-bar': hasColorBar,
        'has-action-display': hasActionDisplay,
      },
    ]"
    :style="eventBlockStyle"
  >
    <button
      class="calendar-matrix-event-block__main"
      type="button"
      :draggable="isDraggable"
      :aria-label="`${event.title}, ${timeDisplay}`"
      @click="handleClick"
      @dragstart="handleDragStart"
      @dragend="handleDragEnd"
    >
      <slot :event="event" :display="display" :time-display="timeDisplay">
        <div class="calendar-matrix-event-block__title">
          <slot name="header" :event="event" :display="display" :time-display="timeDisplay">
            {{ event.title }}
          </slot>
        </div>
        <div class="calendar-matrix-event-block__meta">{{ timeDisplay }}</div>
      </slot>
    </button>
    <button
      v-if="isActionClickable"
      class="calendar-matrix-event-block__action"
      type="button"
      :aria-label="display?.action?.ariaLabel ?? display?.action?.text ?? 'Event action'"
      @click.stop="handleActionClick"
      @keydown.enter.stop.prevent="handleActionClick"
      @keydown.space.stop.prevent="handleActionClick"
    >
      <v-icon v-if="display?.action?.icon" :icon="display.action.icon" size="16" />
      <span v-else>{{ display?.action?.text }}</span>
    </button>
    <span v-else-if="hasActionDisplay" class="calendar-matrix-event-block__action is-static">
      <v-icon v-if="display?.action?.icon" :icon="display.action.icon" size="16" />
      <span v-else>{{ display?.action?.text }}</span>
    </span>
  </article>
</template>

<style scoped>
.calendar-matrix-event-block {
  --calendar-event-primary-bg: rgb(var(--v-theme-surface-light));
  --calendar-event-secondary-bg: rgb(var(--v-theme-secondary) / 0.08);
  --calendar-event-muted-bg: rgb(var(--v-theme-surface-variant));
  --calendar-event-warning-bg: rgb(var(--v-theme-warning) / 0.12);
  --calendar-event-bg: var(--calendar-event-primary-bg);
  background: linear-gradient(var(--calendar-event-bg), var(--calendar-event-bg)), rgb(var(--v-theme-surface));
  border: 1px solid var(--calendar-event-border-color, var(--ua-text-secondary));
  border-radius: 4px;
  color: var(--ua-text-primary);
  display: grid;
  align-content: start;
  overflow: visible;
  padding: 0.375rem 0.5rem;
  position: relative;
}

.calendar-matrix-event-block__main {
  background: transparent;
  border: 0;
  color: inherit;
  cursor: pointer;
  display: grid;
  gap: 0.275rem;
  padding: 0;
  text-align: left;
  width: 100%;
}

.calendar-matrix-event-block__main:focus-visible {
  outline: 2px solid rgb(var(--v-theme-primary) / 0.35);
  outline-offset: 2px;
}

.calendar-matrix-event-block.has-color-bar {
  padding-left: 0.75rem;
}

.calendar-matrix-event-block.has-action-display {
  padding-right: 2rem;
}

.calendar-matrix-event-block.has-color-bar::before {
  background: var(--calendar-event-border-color);
  bottom: 0;
  content: '';
  left: 0;
  position: absolute;
  top: 0;
  width: 4px;
}

.calendar-matrix-event-block__action {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  display: inline-flex;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 1.375rem;
  justify-content: center;
  line-height: 1;
  padding: 0;
  position: absolute;
  right: 0.375rem;
  top: 0.375rem;
  width: 1.375rem;
}

button.calendar-matrix-event-block__action {
  cursor: pointer;
}

.calendar-matrix-event-block__action.is-static {
  pointer-events: none;
}

.calendar-matrix-event-block.is-draggable {
  cursor: grab;
}

.calendar-matrix-event-block.is-draggable .calendar-matrix-event-block__main {
  cursor: grab;
}

.calendar-matrix-event-block.is-draft {
  border-style: dashed;
}

.calendar-matrix-event-block.is-active {
  border-style: solid;
}

.calendar-matrix-event-block.is-cancelled {
  border-style: dotted;
  opacity: 0.6;
  text-decoration: line-through;
}

.calendar-matrix-event-block.is-selected {
  box-shadow: 0 0 0 2px rgb(var(--v-theme-primary) / 0.28);
}

@media (hover: hover) and (pointer: fine) {
  .calendar-matrix-event-block:hover {
    box-shadow: 0 0 0 2px rgb(var(--v-theme-primary) / 0.18);
  }

  button.calendar-matrix-event-block__action:hover {
    background: rgb(var(--v-theme-primary) / 0.08);
    border-color: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-primary));
  }
}

.calendar-matrix-event-block.has-primary-variant {
  --calendar-event-bg: var(--calendar-event-primary-bg);
}

.calendar-matrix-event-block.has-secondary-variant {
  --calendar-event-bg: var(--calendar-event-secondary-bg);
}

.calendar-matrix-event-block.has-muted-variant {
  --calendar-event-bg: var(--calendar-event-muted-bg);
}

.calendar-matrix-event-block.has-warning-variant {
  --calendar-event-bg: var(--calendar-event-warning-bg);
}

.calendar-matrix-event-block__title {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  line-height: 1.25;
}

.calendar-matrix-event-block__meta {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  line-height: 1.2;
}
</style>
