<script setup lang="ts">
import type { CalendarConflict, CalendarConflictEvent } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixConflictItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import {
  formatCalendarConflictEventDateTime,
  resolveCalendarConflictSides,
} from '@/modules/calendar/calendarConflictFormatting';

const props = defineProps<{
  conflicts: CalendarMatrixConflictItem[];
  icon?: string;
  timeZone: string;
}>();

const emit = defineEmits<{
  resolve: [item: CalendarMatrixConflictItem];
}>();

function conflictingEvent(item: CalendarMatrixConflictItem) {
  return resolveCalendarConflictSides(item.conflict, item.currentEventId).other;
}

function timeLabel(event: CalendarConflictEvent) {
  return formatCalendarConflictEventDateTime(event, props.timeZone);
}

function resolveLabel(conflict: CalendarConflict) {
  return conflict.isOverridden ? 'View resolution' : 'Resolve';
}
</script>

<template>
  <section class="calendar-scheduling-conflict-overlay">
    <h3 class="calendar-scheduling-conflict-overlay__heading">Conflict(s)</h3>
    <article v-for="item in conflicts" :key="item.conflict.id" class="calendar-scheduling-conflict-overlay__item">
      <div class="calendar-scheduling-conflict-overlay__summary">
        <v-icon
          v-if="icon"
          :icon="icon"
          size="18"
          :class="{ 'calendar-scheduling-conflict-overlay__icon--overridden': item.conflict.isOverridden }"
        />
        <div>
          <strong :class="{ 'calendar-scheduling-conflict-overlay__title--overridden': item.conflict.isOverridden }">
            {{ conflictingEvent(item).title }}
          </strong>
          <span>{{ timeLabel(conflictingEvent(item)) }}</span>
        </div>
      </div>
      <button
        class="calendar-scheduling-conflict-overlay__resolve"
        :class="{ 'calendar-scheduling-conflict-overlay__resolve--overridden': item.conflict.isOverridden }"
        type="button"
        @click.stop="emit('resolve', item)"
      >
        {{ resolveLabel(item.conflict) }}
      </button>
    </article>
  </section>
</template>

<style scoped>
.calendar-scheduling-conflict-overlay {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-text-primary);
  border-radius: 4px;
  color: var(--ua-text-primary);
  display: grid;
  gap: var(--ua-spacing-sm);
  left: 0;
  padding: var(--ua-spacing-sm);
  position: absolute;
  right: 0;
  top: 1.875rem;
  z-index: 20;
}

.calendar-scheduling-conflict-overlay__item {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.calendar-scheduling-conflict-overlay__heading {
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-scheduling-conflict-overlay__icon--overridden,
.calendar-scheduling-conflict-overlay__title--overridden {
  color: rgb(var(--v-theme-warning));
}

.calendar-scheduling-conflict-overlay__summary {
  align-items: center;
  color: rgb(var(--v-theme-error));
  display: flex;
  font-size: var(--ua-font-size-sm);
  gap: var(--ua-spacing-xs);
  line-height: 1.25;
}

.calendar-scheduling-conflict-overlay__summary div {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.calendar-scheduling-conflict-overlay__summary span {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
}

.calendar-scheduling-conflict-overlay__resolve {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgb(var(--v-theme-error));
  border-radius: 4px;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-sm);
  line-height: 1.25;
  padding: var(--ua-spacing-sm);
}

.calendar-scheduling-conflict-overlay__resolve--overridden {
  background: rgb(var(--v-theme-warning) / 0.12);
  border-color: rgb(var(--v-theme-warning));
}
</style>
