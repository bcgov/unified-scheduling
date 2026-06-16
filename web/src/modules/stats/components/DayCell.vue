<script setup lang="ts">
import type { DaySummary } from '../types';

defineProps<{
  date: string;
  dayName: string;
  dayNumber: number;
  summary: DaySummary;
  isSelected: boolean;
  isToday: boolean;
}>();

const emit = defineEmits<{
  click: [];
}>();

function formatHours(h: number): string {
  return h % 1 === 0 ? `${h}h` : `${h.toFixed(1)}h`;
}
</script>

<template>
  <button
    class="day-cell"
    :class="{
      'day-cell--selected': isSelected,
      'day-cell--today': isToday,
      'day-cell--has-entries': summary.assignmentCount > 0,
    }"
    type="button"
    @click="emit('click')"
  >
    <span class="day-cell__name">{{ dayName }}</span>
    <span class="day-cell__number">{{ dayNumber }}</span>
    <div class="day-cell__summary">
      <span v-if="summary.regularHours > 0" class="day-cell__hours day-cell__hours--regular">
        {{ formatHours(summary.regularHours) }}
      </span>
      <span v-if="summary.overtimeHours > 0" class="day-cell__hours day-cell__hours--overtime">
        +{{ formatHours(summary.overtimeHours) }} OT
      </span>
      <span v-if="summary.assignmentCount === 0" class="day-cell__empty">—</span>
    </div>
  </button>
</template>

<style scoped>
.day-cell {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--ua-spacing-xs);
  padding: var(--ua-spacing-sm);
  border: 2px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: var(--ua-card-body-bg);
  cursor: pointer;
  width: 100%;
  transition:
    border-color 0.15s,
    background 0.15s;
  min-height: 90px;
}

.day-cell:hover {
  border-color: rgb(var(--v-theme-primary));
  background: rgb(var(--v-theme-surface));
}

.day-cell--selected {
  border-color: #fcba19;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 0 0 2px #fcba19;
}

.day-cell--today .day-cell__number {
  background: rgb(var(--v-theme-primary));
  color: #fff;
  border-radius: 50%;
  width: 26px;
  height: 26px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.day-cell__name {
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.day-cell__number {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.day-cell__summary {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2px;
  min-height: 32px;
}

.day-cell__hours {
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  border-radius: 4px;
  padding: 1px 6px;
}

.day-cell__hours--regular {
  background: rgba(66, 129, 74, 0.15);
  color: #42814a;
}

.day-cell__hours--overtime {
  background: rgba(206, 62, 57, 0.12);
  color: #ce3e39;
}

.day-cell__empty {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-muted);
}
</style>
