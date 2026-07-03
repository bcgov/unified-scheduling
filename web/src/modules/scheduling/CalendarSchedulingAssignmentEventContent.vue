<script setup lang="ts">
import { computed } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { getCalendarAssignedUsers, getCalendarAssignmentCapacity, type CalendarUser } from './calendarSchedulingData';

const props = defineProps<{
  event: CalendarEventBase;
}>();

const capacityDisplay = computed(
  () =>
    getCalendarAssignmentCapacity(props.event) ?? {
      capacity: 0,
      assignedCount: 0,
      filledCount: 0,
      overflowCount: 0,
    },
);
const capacitySlots = computed(() =>
  Array.from({ length: capacityDisplay.value.capacity }, (_value, index) => index + 1),
);
const visibleAssignedUsers = computed(() => getCalendarAssignedUsers(props.event).slice(0, 2));
const assignedUserOverflowCount = computed(() => Math.max(getCalendarAssignedUsers(props.event).length - 2, 0));

function formatAssignedUserName(user: CalendarUser) {
  const parts = user.title.trim().split(/\s+/);
  const firstName = parts[0] ?? '';
  const lastName = parts.at(-1) ?? user.title;

  return firstName ? `${firstName.charAt(0)}. ${lastName}` : lastName;
}
</script>

<template>
  <div class="calendar-assignment-block">
    <div
      class="calendar-scheduling-capacity"
      :aria-label="`${capacityDisplay.assignedCount} of ${capacityDisplay.capacity} assignment slots filled`"
    >
      <span
        v-for="slot in capacitySlots"
        :key="slot"
        class="calendar-scheduling-capacity__slot"
        :class="{ 'is-filled': slot <= capacityDisplay.filledCount }"
      />
      <span v-if="capacityDisplay.overflowCount" class="calendar-scheduling-capacity__overflow">
        +{{ capacityDisplay.overflowCount }}
      </span>
    </div>

    <div v-if="visibleAssignedUsers.length" class="calendar-scheduling-assigned-users">
      <span v-for="user in visibleAssignedUsers" :key="user.id" class="calendar-scheduling-assigned-users__name">
        {{ formatAssignedUserName(user) }}
      </span>
      <span v-if="assignedUserOverflowCount" class="calendar-scheduling-assigned-users__overflow">
        +{{ assignedUserOverflowCount }} more
      </span>
    </div>
  </div>
</template>

<style scoped>
.calendar-assignment-block {
  display: grid;
  gap: var(--ua-spacing-sm);
  min-block-size: 3.25rem;
}

.calendar-scheduling-capacity {
  align-items: start;
  display: flex;
  gap: 0.1875rem;
  min-block-size: 1rem;
}

.calendar-scheduling-capacity__slot {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-text-secondary);
  border-radius: 2px;
  display: inline-block;
  height: 0.625rem;
  width: 0.625rem;
}

.calendar-scheduling-capacity__slot.is-filled {
  background: var(--calendar-event-border-color, var(--ua-text-secondary));
  border-color: var(--calendar-event-border-color, var(--ua-text-secondary));
}

.calendar-scheduling-capacity__overflow {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  line-height: 1;
  margin-left: 0.125rem;
}

.calendar-scheduling-assigned-users {
  align-self: end;
  color: var(--ua-text-secondary);
  display: flex;
  flex-wrap: wrap;
  font-size: var(--ua-font-size-xs);
  gap: 0.3625rem;
  line-height: 1.2;
}

.calendar-scheduling-assigned-users::before {
  content: '- ';
}

.calendar-scheduling-assigned-users__name,
.calendar-scheduling-assigned-users__overflow {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.calendar-scheduling-assigned-users__overflow {
  color: var(--ua-text-primary);
  font-weight: var(--ua-font-weight-semibold);
}
</style>
