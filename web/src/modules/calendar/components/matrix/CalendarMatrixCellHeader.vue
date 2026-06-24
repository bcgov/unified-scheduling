<script setup lang="ts">
import { computed } from 'vue';
import { resolveMatrixStatusClass, toRgba } from './calendarMatrixDisplayUtils';
import { CalendarMatrixActionType } from './calendarMatrixTypes';
import type {
  CalendarMatrixCell,
  CalendarMatrixCellHeader,
  CalendarMatrixCellHeaderActionEvent,
} from './calendarMatrixTypes';

const props = defineProps<{
  cell: CalendarMatrixCell;
  header: CalendarMatrixCellHeader;
  selected?: boolean;
}>();

const emit = defineEmits<{
  click: [header: CalendarMatrixCellHeader];
  action: [payload: CalendarMatrixCellHeaderActionEvent];
}>();

defineSlots<{
  header?: (props: { cell: CalendarMatrixCell; header: CalendarMatrixCellHeader }) => unknown;
}>();

const isClickable = computed(() => Boolean(props.header.actionId));
const hasActionDisplay = computed(() => !!props.header.action?.icon || !!props.header.action?.text);
const isActionClickable = computed(
  () => hasActionDisplay.value && props.header.action?.type !== CalendarMatrixActionType.Custom,
);

const statusClass = computed(() => resolveMatrixStatusClass(props.header.status));
const hasColor = computed(() => Boolean(props.header.color));

const headerStyle = computed(() => {
  if (!props.header.color) {
    return undefined;
  }

  return {
    '--calendar-matrix-cell-header-background-color': toRgba(props.header.color, 0.12),
    '--calendar-matrix-cell-header-border-color': props.header.color,
  };
});

function handleClick() {
  if (!isClickable.value) {
    return;
  }

  emit('click', props.header);
}

function handleActionClick() {
  const action = props.header.action;

  if (!isActionClickable.value || !action) {
    return;
  }

  emit('action', {
    cell: props.cell,
    header: props.header,
    actionId: action.actionId,
    actionType: action.type,
  });
}

</script>

<template>
  <article
    class="calendar-matrix-cell-header"
    :class="[
      selected ? 'is-selected' : undefined,
      hasColor ? 'has-color' : undefined,
      statusClass ? `is-${statusClass}` : undefined,
      hasActionDisplay ? 'has-action-display' : undefined,
    ]"
    :style="headerStyle"
    :title="header.title ?? header.text"
  >
    <button v-if="isClickable" type="button" class="calendar-matrix-cell-header__main" @click="handleClick">
      <span class="calendar-matrix-cell-header__text">
        <slot name="header" :cell="cell" :header="header">
          {{ header.text }}
        </slot>
      </span>
    </button>
    <div v-else class="calendar-matrix-cell-header__main">
      <span class="calendar-matrix-cell-header__text">
        <slot name="header" :cell="cell" :header="header">
          {{ header.text }}
        </slot>
      </span>
    </div>

    <button
      v-if="isActionClickable"
      class="calendar-matrix-cell-header__action"
      type="button"
      :aria-label="header.action?.ariaLabel ?? header.action?.text ?? 'Header action'"
      @click.stop="handleActionClick"
      @keydown.enter.stop.prevent="handleActionClick"
      @keydown.space.stop.prevent="handleActionClick"
    >
      <v-icon v-if="header.action?.icon" :icon="header.action.icon" size="16" />
      <span v-else>{{ header.action?.text }}</span>
    </button>
    <span v-else-if="hasActionDisplay" class="calendar-matrix-cell-header__action is-static">
      <v-icon v-if="header.action?.icon" :icon="header.action.icon" size="16" />
      <span v-else>{{ header.action?.text }}</span>
    </span>
  </article>
</template>

<style scoped>
.calendar-matrix-cell-header {
  align-items: center;
  background: var(--calendar-matrix-cell-header-background-color, var(--bs-tertiary-bg));
  border: 1px solid var(--calendar-matrix-cell-header-border-color, var(--ua-text-secondary));
  border-radius: 0.375rem;
  color: var(--ua-text-primary);
  display: grid;
  font: inherit;
  font-size: 0.8125rem;
  font-weight: 600;
  inline-size: 100%;
  line-height: 1.2;
  min-block-size: 1.75rem;
  padding: 0.25rem 0.5rem;
  position: relative;
  text-align: start;
}

.calendar-matrix-cell-header.has-action-display {
  padding-right: 2rem;
}

.calendar-matrix-cell-header__main {
  align-items: center;
  background: transparent;
  border: 0;
  color: inherit;
  display: flex;
  font: inherit;
  font-size: inherit;
  font-weight: inherit;
  line-height: inherit;
  min-inline-size: 0;
  padding: 0;
  text-align: start;
}

button.calendar-matrix-cell-header__main {
  cursor: pointer;
}

.calendar-matrix-cell-header:has(button.calendar-matrix-cell-header__main:hover) {
  background: var(--calendar-matrix-cell-header-background-color, var(--bs-secondary-bg));
}

button.calendar-matrix-cell-header__main:focus-visible {
  outline: 2px solid var(--bs-primary);
  outline-offset: 2px;
}

.calendar-matrix-cell-header__action {
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
  right: 0.25rem;
  top: 0.1875rem;
  width: 1.375rem;
}

button.calendar-matrix-cell-header__action {
  cursor: pointer;
}

.calendar-matrix-cell-header__action.is-static {
  pointer-events: none;
}

.calendar-matrix-cell-header.is-selected {
  box-shadow: 0 0 0 2px rgb(var(--v-theme-primary) / 0.28);
}

.calendar-matrix-cell-header.is-draft {
  border-style: dashed;
}

.calendar-matrix-cell-header.is-active {
  border-style: solid;
}

.calendar-matrix-cell-header.is-cancelled {
  border-style: dotted;
  opacity: 0.6;
  text-decoration: line-through;
}

.calendar-matrix-cell-header__text {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (hover: hover) and (pointer: fine) {
  button.calendar-matrix-cell-header__action:hover {
    background: rgb(var(--v-theme-primary) / 0.08);
    border-color: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-primary));
  }
}
</style>
