<script setup lang="ts">
import type { CalendarMatrixDay } from './calendarMatrixTypes';

defineProps<{
  primaryColumnLabel: string;
  days: CalendarMatrixDay[];
}>();
</script>

<template>
  <div class="calendar-matrix-header" role="row">
    <div class="calendar-matrix-header__primary" role="columnheader">{{ primaryColumnLabel }}</div>
    <div
      v-for="day in days"
      :key="day.date"
      class="calendar-matrix-header__day"
      role="columnheader"
      :aria-label="day.date"
      :class="{ 'is-today': day.isToday }"
    >
      {{ day.label }}
    </div>
  </div>
</template>

<style scoped>
.calendar-matrix-header {
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid var(--ua-border-color);
  display: contents;
}

.calendar-matrix-header__primary,
.calendar-matrix-header__day {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  color: var(--ua-text-secondary);
  display: flex;
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  padding: var(--ua-spacing-sm) var(--ua-spacing-md);
  text-transform: uppercase;
}

.calendar-matrix-header__primary {
  border-bottom: 1px solid var(--ua-border-color);
  left: 0;
  position: sticky;
  z-index: 3;
}

.calendar-matrix-header__day {
  border-bottom: 1px solid var(--ua-border-color);
  border-inline-start: 1px solid var(--ua-border-color);
  justify-content: center;
  text-align: center;
}

.calendar-matrix-header__day.is-today {
  background:
    linear-gradient(var(--calendar-today-bg-color), var(--calendar-today-bg-color)) padding-box,
    rgb(var(--v-theme-surface)) border-box;
  color: var(--ua-text-primary);
}
</style>
