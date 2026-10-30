<script setup lang="ts">
import { mdiHours24 } from '@mdi/js';
import { computed } from 'vue';
import { CALENDAR_ALL_DAY_LABEL } from '@/utils/date';
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
const infoIcons = computed(() => [
  ...(props.header.allDay
    ? [{ icon: mdiHours24, ariaLabel: CALENDAR_ALL_DAY_LABEL, title: CALENDAR_ALL_DAY_LABEL }]
    : []),
  ...(props.header.info?.icons ?? []),
]);
const subtitle = computed(() =>
  props.header.allDay && props.header.subtitle === CALENDAR_ALL_DAY_LABEL ? undefined : props.header.subtitle,
);
const hasInfoIcons = computed(() => infoIcons.value.length > 0);
const hasActionDisplay = computed(() => !!props.header.action?.icon || !!props.header.action?.text);
const isActionClickable = computed(
  () => hasActionDisplay.value && props.header.action?.type !== CalendarMatrixActionType.Custom,
);

const statusClass = computed(() => resolveMatrixStatusClass(props.header.status));
const hasColor = computed(() => Boolean(props.header.color));
const hasDottedBorder = computed(() => props.header.borderStyle === 'dotted');

const headerStyle = computed(() => {
  if (props.header.variant === 'outlined') {
    return {
      '--calendar-matrix-cell-header-border-color': props.header.color ?? 'var(--ua-border-color)',
    };
  }

  if (!props.header.color || statusClass.value === 'active') {
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
      header.allDay ? 'is-all-day' : undefined,
      header.variant === 'outlined' ? 'is-outlined' : undefined,
      hasColor ? 'has-color' : undefined,
      hasDottedBorder ? 'has-dotted-border' : undefined,
      statusClass ? `is-${statusClass}` : undefined,
      hasInfoIcons ? 'has-info-icons' : undefined,
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
      <span v-if="subtitle" class="calendar-matrix-cell-header__subtitle">{{ subtitle }}</span>
    </button>
    <div v-else class="calendar-matrix-cell-header__main">
      <span class="calendar-matrix-cell-header__text">
        <slot name="header" :cell="cell" :header="header">
          {{ header.text }}
        </slot>
      </span>
      <span v-if="subtitle" class="calendar-matrix-cell-header__subtitle">{{ subtitle }}</span>
    </div>

    <span v-if="hasInfoIcons" class="calendar-matrix-cell-header__info-icons">
      <span
        v-for="infoIcon in infoIcons"
        :key="`${infoIcon.icon}-${infoIcon.ariaLabel ?? infoIcon.title ?? ''}`"
        class="calendar-matrix-cell-header__info-icon"
        :role="infoIcon.ariaLabel ? 'img' : undefined"
        :aria-label="infoIcon.ariaLabel"
        :aria-hidden="infoIcon.ariaLabel ? undefined : true"
        :title="infoIcon.title ?? infoIcon.ariaLabel"
      >
        <v-icon :icon="infoIcon.icon" :title="infoIcon.title ?? infoIcon.ariaLabel" size="16" />
      </span>
    </span>

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
  border: var(--bcds-layout-border-width-small) solid
    var(--calendar-matrix-cell-header-border-color, var(--ua-text-secondary));
  border-radius: 0.375rem;
  box-sizing: border-box;
  color: var(--ua-text-primary);
  display: grid;
  font: inherit;
  font-size: 0.8125rem;
  font-weight: 600;
  inline-size: 100%;
  line-height: 1.2;
  min-block-size: 1.75rem;
  min-inline-size: 0;
  padding: 0.25rem 0.5rem;
  position: relative;
  text-align: start;
}

.calendar-matrix-cell-header.has-dotted-border {
  border-style: dotted;
  border-width: var(--bcds-layout-border-width-medium);
}

.calendar-matrix-cell-header.is-outlined {
  --calendar-matrix-cell-header-background-color: rgb(var(--v-theme-surface));
}

.calendar-matrix-cell-header.is-all-day {
  border-radius: 0;
}

.calendar-matrix-cell-header.has-action-display,
.calendar-matrix-cell-header.has-info-icons {
  padding-right: 2rem;
}

.calendar-matrix-cell-header.has-info-icons.has-action-display {
  padding-right: 3.75rem;
}

.calendar-matrix-cell-header__main {
  align-items: stretch;
  background: transparent;
  border: 0;
  color: inherit;
  display: flex;
  flex-direction: column;
  font: inherit;
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

.calendar-matrix-cell-header__info-icons {
  align-items: center;
  color: inherit;
  display: inline-flex;
  gap: 0.125rem;
  pointer-events: none;
  position: absolute;
  right: 0.25rem;
  top: 0.1875rem;
}

.calendar-matrix-cell-header.has-action-display .calendar-matrix-cell-header__info-icons {
  right: 1.875rem;
}

.calendar-matrix-cell-header__info-icon {
  align-items: center;
  color: inherit;
  display: inline-flex;
  height: 1.375rem;
  justify-content: center;
  line-height: 1;
  pointer-events: auto;
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

.calendar-matrix-cell-header.is-active:not(.is-outlined) {
  background: rgba(var(--v-theme-on-surface), 0.82);
  border-color: rgba(var(--v-theme-on-surface), 0.82);
  color: rgb(var(--v-theme-surface));
}

.calendar-matrix-cell-header.is-active:not(.is-outlined) .calendar-matrix-cell-header__action {
  background: transparent;
  border-color: transparent;
  color: rgb(var(--v-theme-surface));
}

.calendar-matrix-cell-header.is-cancelled {
  opacity: 0.6;
  text-decoration: line-through;
}

.calendar-matrix-cell-header__text {
  min-inline-size: 0;
  max-inline-size: 100%;
  overflow-wrap: anywhere;
  white-space: normal;
}

.calendar-matrix-cell-header__subtitle {
  font-size: var(--ua-font-size-xs);
  font-weight: normal;
  line-height: 1.2;
}

@media (hover: hover) and (pointer: fine) {
  .calendar-matrix-cell-header.is-active:not(.is-outlined):has(button.calendar-matrix-cell-header__main:hover) {
    background: rgba(var(--v-theme-on-surface), 0.82);
  }

  button.calendar-matrix-cell-header__action:hover {
    background: rgb(var(--v-theme-primary) / 0.08);
    border-color: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-primary));
  }
}
</style>
