<script setup lang="ts">
import { computed } from 'vue';
import type { CalendarConflict, CalendarConflictEvent, CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { formatCalendarConflictEventDateTime } from '@/modules/calendar/calendarConflictFormatting';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';

const props = defineProps<{
  event: CalendarEventBase;
  conflicts: CalendarConflict[];
  icon?: string;
  timeZone?: string;
}>();

const emit = defineEmits<{
  resolve: [conflict: CalendarConflict];
}>();

const conflictItems = computed(() => {
  if (!isCalendarSchedulingEvent(props.event)) {
    return [];
  }

  const eventId = props.event.metadata.eventId;
  return props.conflicts.flatMap((conflict) => {
    if (conflict.entry.eventId === eventId) {
      return [{ conflict, event: conflict.overlaps }];
    }

    if (conflict.overlaps.eventId === eventId) {
      return [{ conflict, event: conflict.entry }];
    }

    return [];
  });
});

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
    <article v-for="item in conflictItems" :key="item.conflict.id" class="calendar-scheduling-conflict-overlay__item">
      <div class="calendar-scheduling-conflict-overlay__summary">
        <v-icon
          v-if="icon"
          :icon="icon"
          size="18"
          :class="{ 'calendar-scheduling-conflict-overlay__icon--overridden': item.conflict.isOverridden }"
        />
        <div>
          <strong :class="{ 'calendar-scheduling-conflict-overlay__title--overridden': item.conflict.isOverridden }">
            {{ item.event.title }}
          </strong>
          <span>{{ timeLabel(item.event) }}</span>
        </div>
      </div>
      <button
        class="calendar-scheduling-conflict-overlay__resolve"
        :class="{ 'calendar-scheduling-conflict-overlay__resolve--overridden': item.conflict.isOverridden }"
        type="button"
        @click.stop="emit('resolve', item.conflict)"
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
