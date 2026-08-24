<script setup lang="ts">
import { computed } from 'vue';
import { mdiAlert } from '@mdi/js';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import {
  getCalendarAssignedUsers,
  getCalendarAssignmentCapacity,
  isCalendarSchedulingEvent,
  type CalendarAssignmentPartialCoverageShift,
  type CalendarAssignmentCapacitySlotState,
  type CalendarUser,
} from './calendarSchedulingData';

const props = defineProps<{
  event: CalendarEventBase;
  users?: CalendarUser[];
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
const capacitySlotStates = computed<CalendarAssignmentCapacitySlotState[]>(() => {
  if (isCalendarSchedulingEvent(props.event) && props.event.metadata.capacitySlotStates?.length) {
    return props.event.metadata.capacitySlotStates;
  }

  return capacitySlots.value.map((slot) => (slot <= capacityDisplay.value.filledCount ? 'filled' : 'empty'));
});
const assignedUsers = computed(() => resolveAssignedUsers());
const visibleAssignedUsers = computed(() => assignedUsers.value.slice(0, 2));
const assignedUserOverflowCount = computed(() => Math.max(assignedUsers.value.length - 2, 0));
const partialCoverageShifts = computed(() =>
  isCalendarSchedulingEvent(props.event) ? (props.event.metadata.partialCoverageShifts ?? []) : [],
);
const hasPartialCoverage = computed(() => partialCoverageShifts.value.length > 0);
const partialCoverageLines = computed(() =>
  partialCoverageShifts.value.map((shift) => formatPartialCoverageShift(shift)),
);

function resolveAssignedUsers() {
  const usersById = new Map((props.users ?? []).map((user) => [user.id, user]));

  return getCalendarAssignedUsers(props.event).map((user) => usersById.get(user.id) ?? user);
}

function formatAssignedUserName(user: CalendarUser) {
  if (user.title === user.id) {
    return 'Unknown user';
  }

  const parts = user.title.trim().split(/\s+/);
  const firstName = parts[0] ?? '';
  const lastName = parts.at(-1) ?? user.title;

  return firstName ? `${firstName.charAt(0)}. ${lastName}` : lastName;
}

function formatPartialCoverageShift(shift: CalendarAssignmentPartialCoverageShift) {
  const userNames = resolvePartialCoverageUsers(shift).map(formatAssignedUserName).join(', ');
  const timeRange = formatShiftTimeRange(shift);

  return `${userNames || 'Unknown user'} (${timeRange})`;
}

function resolvePartialCoverageUsers(shift: CalendarAssignmentPartialCoverageShift) {
  const assignedUsers = getCalendarAssignedUsers(props.event);
  const usersById = new Map([...assignedUsers, ...(props.users ?? [])].map((user) => [user.id, user]));
  return shift.userIds.map((userId) => usersById.get(userId) ?? ({ id: userId, type: 'user', title: userId } as const));
}

function formatShiftTimeRange(shift: CalendarAssignmentPartialCoverageShift) {
  const start = formatShiftTime(shift.start, shift.timeZoneId);
  const end = formatShiftTime(shift.end, shift.timeZoneId);

  if (start && end) {
    return `${start} - ${end}`;
  }

  return start || end || 'unknown time';
}

function formatShiftTime(value?: string, timeZoneId?: string) {
  if (!value) {
    return '';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return '';
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: 'numeric',
    minute: '2-digit',
    timeZone: timeZoneId,
  }).format(date);
}
</script>

<template>
  <div class="calendar-assignment-block">
    <div
      class="calendar-scheduling-capacity"
      :aria-label="`${capacityDisplay.assignedCount} Of ${capacityDisplay.capacity} Assignment Slots Filled`"
    >
      <span
        v-for="slot in capacitySlots"
        :key="slot"
        class="calendar-scheduling-capacity__slot"
        :class="`is-${capacitySlotStates[slot - 1] ?? 'empty'}`"
      />
      <span
        v-if="capacityDisplay.overflowCount"
        class="calendar-scheduling-capacity__overflow"
        title="capacity exceeded"
      >
        +{{ capacityDisplay.overflowCount }}
      </span>
      <v-menu v-if="hasPartialCoverage" open-on-hover location="top" :close-delay="100">
        <template #activator="{ props: activatorProps }">
          <button
            v-bind="activatorProps"
            class="calendar-scheduling-capacity__partial-warning"
            type="button"
            aria-label="Partial Coverage"
          >
            <v-icon :icon="mdiAlert" size="14" />
          </button>
        </template>
        <div class="calendar-scheduling-capacity__partial-popover" role="tooltip">
          <p class="calendar-scheduling-capacity__partial-heading">Partial coverage:</p>
          <p v-for="line in partialCoverageLines" :key="line" class="calendar-scheduling-capacity__partial-line">
            {{ line }}
          </p>
        </div>
      </v-menu>
    </div>

    <div v-if="visibleAssignedUsers.length" class="calendar-scheduling-assigned-users">
      <span v-for="user in visibleAssignedUsers" :key="user.id" class="calendar-scheduling-assigned-users__name">
        - {{ formatAssignedUserName(user) }}
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
  min-width: 0;
}

.calendar-scheduling-capacity {
  align-items: center;
  display: flex;
  gap: 0.1875rem;
  min-block-size: 1rem;
}

.calendar-scheduling-capacity__slot {
  background: var(--ua-scheduling-capacity-slot-empty-bg);
  border: 1px solid var(--ua-text-secondary);
  border-radius: 2px;
  display: inline-block;
  height: 0.75rem;
  width: 0.75rem;
}

.calendar-scheduling-capacity__slot.is-filled {
  background: var(--ua-scheduling-capacity-slot-filled-bg);
}

.calendar-scheduling-capacity__slot.is-partial {
  background: var(--ua-scheduling-capacity-slot-partial-bg);
}

.calendar-scheduling-capacity__overflow {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  line-height: 1;
  margin-left: 0.125rem;
}

.calendar-scheduling-capacity__partial-warning {
  align-self: center;
  align-items: center;
  background: transparent;
  border: 0;
  color: var(--ua-scheduling-capacity-slot-partial-bg);
  cursor: pointer;
  display: inline-flex;
  height: 0.75rem;
  justify-content: center;
  margin-left: 0.125rem;
  padding: 0;
  width: 0.75rem;
}

.calendar-scheduling-capacity__partial-popover {
  background: var(--ua-field-bg);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  color: var(--ua-text-primary);
  display: grid;
  font-size: var(--ua-font-size-xs);
  gap: var(--ua-spacing-xs);
  max-width: 18rem;
  padding: var(--ua-spacing-sm);
}

.calendar-scheduling-capacity__partial-heading,
.calendar-scheduling-capacity__partial-line {
  margin: 0;
}

.calendar-scheduling-capacity__partial-heading {
  font-weight: var(--ua-font-weight-bold);
}

.calendar-scheduling-assigned-users {
  align-self: end;
  color: var(--ua-text-secondary);
  display: flex;
  flex-wrap: wrap;
  font-size: var(--ua-font-size-xs);
  gap: 0.3625rem;
  line-height: 1.2;
  min-width: 0;
  overflow: hidden;
}

.calendar-scheduling-assigned-users__name,
.calendar-scheduling-assigned-users__overflow {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.calendar-scheduling-assigned-users__name {
  max-width: 100%;
}

.calendar-scheduling-assigned-users__overflow {
  color: var(--ua-text-primary);
  font-weight: var(--ua-font-weight-semibold);
}
</style>
