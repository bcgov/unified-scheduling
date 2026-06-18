<script setup lang="ts">
import { computed, ref } from 'vue';
import type { DaySummary } from '../types';
import { DAILY_REGULAR_TARGET_HOURS, WEEKLY_REGULAR_TARGET_HOURS } from '../constants';
import DayCell from './DayCell.vue';

const props = defineProps<{
  weekDates: string[]; // 7 ISO date strings Mon–Sun
  selectedDate: string | null;
  daySummaryMap: Record<string, DaySummary>;
  weeklyRegularTotal: number;
  weeklyOvertimeTotal: number;
}>();

const emit = defineEmits<{
  'select-day': [date: string];
}>();

const DAY_NAMES = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const WEEKEND_INDICES = new Set([5, 6]);

const today = new Date().toISOString().slice(0, 10);
const showWeekends = ref(false);

const dayInfos = computed(() =>
  props.weekDates
    .map((date, i) => {
      const d = new Date(date + 'T00:00:00');
      return {
        date,
        dayName: DAY_NAMES[i],
        dayNumber: d.getDate(),
        isWeekend: WEEKEND_INDICES.has(i),
        summary: props.daySummaryMap[date] ?? { regularHours: 0, overtimeHours: 0, assignmentCount: 0 },
        isSelected: date === props.selectedDate,
        isToday: date === today,
      };
    })
    .filter((info) => showWeekends.value || !info.isWeekend),
);

const gridColumns = computed(() => `repeat(${dayInfos.value.length}, 1fr)`);

function regularBar(date: string): number {
  const h = props.daySummaryMap[date]?.regularHours ?? 0;
  return Math.min((h / DAILY_REGULAR_TARGET_HOURS) * 100, 100);
}
</script>

<template>
  <div class="weekly-grid">
    <!-- Header: week summary + toggle -->
    <div class="weekly-grid__header">
      <div class="weekly-grid__week-summary">
        <span>
          Regular:
          <strong :class="{ 'text-success': weeklyRegularTotal >= WEEKLY_REGULAR_TARGET_HOURS }"> {{ weeklyRegularTotal }}h </strong>
          / {{ WEEKLY_REGULAR_TARGET_HOURS }}h
        </span>
        <span v-if="weeklyOvertimeTotal > 0" class="overtime-total">
          Overtime: <strong>{{ weeklyOvertimeTotal }}h</strong>
        </span>
      </div>
      <button
        class="weekend-toggle"
        :class="{ 'weekend-toggle--active': showWeekends }"
        @click="showWeekends = !showWeekends"
      >
        {{ showWeekends ? 'Hide Weekends' : 'Show Weekends' }}
      </button>
    </div>

    <!-- Day cells -->
    <div class="weekly-grid__cells" :style="{ gridTemplateColumns: gridColumns }">
      <DayCell
        v-for="info in dayInfos"
        :key="info.date"
        :date="info.date"
        :day-name="info.dayName"
        :day-number="info.dayNumber"
        :summary="info.summary"
        :is-selected="info.isSelected"
        :is-today="info.isToday"
        @click="emit('select-day', info.date)"
      />
    </div>

    <!-- Daily totals bar row -->
    <div class="weekly-grid__totals" :style="{ gridTemplateColumns: gridColumns }">
      <div v-for="info in dayInfos" :key="info.date" class="daily-bar">
        <div class="daily-bar__track">
          <div
            class="daily-bar__fill"
            :style="{ width: regularBar(info.date) + '%' }"
            :class="{ 'daily-bar__fill--full': (daySummaryMap[info.date]?.regularHours ?? 0) >= DAILY_REGULAR_TARGET_HOURS }"
          />
        </div>
        <span class="daily-bar__label">
          {{ (daySummaryMap[info.date]?.regularHours ?? 0) > 0 ? `${daySummaryMap[info.date]?.regularHours}h` : '0h' }}
        </span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.weekly-grid {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
}

.weekly-grid__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ua-spacing-md);
}

.weekly-grid__week-summary {
  display: flex;
  gap: var(--ua-spacing-lg);
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
}

.weekend-toggle {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-secondary);
  background: none;
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  padding: 4px 10px;
  cursor: pointer;
  white-space: nowrap;
  transition:
    border-color 0.15s,
    color 0.15s;
}

.weekend-toggle:hover {
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-primary));
}

.weekend-toggle--active {
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.06);
}

.weekly-grid__cells {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.weekly-grid__totals {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.daily-bar {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.daily-bar__track {
  width: 100%;
  height: 6px;
  background: var(--ua-border-color);
  border-radius: 3px;
  overflow: hidden;
}

.daily-bar__fill {
  height: 100%;
  background: rgb(var(--v-theme-primary));
  border-radius: 3px;
  transition: width 0.2s;
}

.daily-bar__fill--full {
  background: var(--ua-stats-color-regular);
}

.daily-bar__label {
  font-size: var(--ua-font-size-xs);
  color: var(--ua-text-secondary);
}

.text-success {
  color: var(--ua-stats-color-regular);
}

.overtime-total {
  color: var(--ua-stats-color-overtime);
}

/* Mobile: horizontal scroll */
@media (max-width: 640px) {
  .weekly-grid__cells,
  .weekly-grid__totals {
    display: flex;
    overflow-x: auto;
    scroll-snap-type: x mandatory;
    gap: var(--ua-spacing-sm);
    padding-bottom: var(--ua-spacing-xs);
    -webkit-overflow-scrolling: touch;
  }

  .weekly-grid__cells > *,
  .weekly-grid__totals > * {
    scroll-snap-align: center;
    min-width: 72px;
    flex-shrink: 0;
  }
}
</style>
