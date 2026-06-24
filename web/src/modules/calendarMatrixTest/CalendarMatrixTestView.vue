<script setup lang="ts">
import { computed } from 'vue';
import UaModal from '@/shared/components/UaModal.vue';
import CalendarMatrixCellHeader from '@/modules/calendar/components/matrix/CalendarMatrixCellHeader.vue';
import CalendarMatrixEventBlock from '@/modules/calendar/components/matrix/CalendarMatrixEventBlock.vue';
import CalendarMatrixView from '@/modules/calendar/components/matrix/CalendarMatrixView.vue';
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import {
  CalendarMatrixActionType,
  type CalendarMatrixCell,
  type CalendarMatrixCellHeader as CalendarMatrixCellHeaderModel,
  type CalendarMatrixCellHeaderActionEvent,
  type CalendarMatrixEventBlockActionEvent,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import {
  calendarMatrixTestConflictEventId,
  calendarMatrixTestDetailEvent,
  calendarMatrixTestDropModalState,
  calendarMatrixTestEventActionEvent,
  calendarMatrixTestResourceActionResource,
  closeCalendarMatrixTestAssignmentModal,
  closeCalendarMatrixTestDropModal,
  closeCalendarMatrixTestEventActionModal,
  closeCalendarMatrixTestEventDetail,
  closeCalendarMatrixTestResourceActionModal,
  isCalendarMatrixTestAssignmentModalOpen,
} from './calendarMatrixTestState';
import {
  getCalendarMatrixTestAssignedUsers,
  getCalendarMatrixTestAssignmentCapacity,
  type CalendarMatrixTestUser,
} from './calendarMatrixTestData';
import { calendarMatrixTestActionIds } from './calendarMatrixTestActionIds';

defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
  showAssignmentContent?: boolean;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

const sourceDetails = computed(() => {
  const drag = calendarMatrixTestDropModalState.value?.drag;

  if (!drag) {
    return [];
  }

  return [
    { name: 'Source', value: drag.source },
    { name: 'Item ID', value: drag.itemId },
    { name: 'Item Type', value: drag.itemType },
    { name: 'Payload', value: formatValue(drag.payload) },
  ];
});

const targetDetails = computed(() => {
  const drop = calendarMatrixTestDropModalState.value?.drop;

  if (!drop) {
    return [];
  }

  return [
    { name: 'Resource ID', value: formatValue(drop.resourceId) },
    { name: 'Resource Type', value: formatValue(drop.resourceType) },
    { name: 'Date', value: formatValue(drop.date) },
  ];
});

const eventDetails = computed(() => {
  const event = calendarMatrixTestDetailEvent.value;

  if (!event) {
    return [];
  }

  return Object.entries(event).map(([name, value]) => ({
    name: formatLabel(name),
    value: formatValue(value),
  }));
});

const resourceDetails = computed(() => {
  const resource = calendarMatrixTestResourceActionResource.value;

  if (!resource) {
    return [];
  }

  return Object.entries(resource).map(([name, value]) => ({
    name: formatLabel(name),
    value: formatValue(value),
  }));
});

const eventActionDetails = computed(() => {
  const event = calendarMatrixTestEventActionEvent.value;

  if (!event) {
    return [];
  }

  return Object.entries(event).map(([name, value]) => ({
    name: formatLabel(name),
    value: formatValue(value),
  }));
});

function formatLabel(value: string) {
  return value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (firstLetter) => firstLetter.toUpperCase());
}

function formatValue(value: unknown) {
  if (value === undefined || value === null || value === '') {
    return 'None';
  }

  if (typeof value === 'string') {
    return value;
  }

  return JSON.stringify(value);
}

function resolveConflict(
  event: CalendarEventBase,
  onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => void,
) {
  onEventAction({
    event,
    actionId: calendarMatrixTestActionIds.resolveConflict,
    actionType: CalendarMatrixActionType.Button,
  });
}

function resolveHeaderConflict(
  cell: CalendarMatrixCell,
  header: CalendarMatrixCellHeaderModel,
  onHeaderAction: (payload: CalendarMatrixCellHeaderActionEvent) => void,
) {
  onHeaderAction({
    cell,
    header,
    actionId: calendarMatrixTestActionIds.resolveConflict,
    actionType: CalendarMatrixActionType.Button,
  });
}

function getCapacityDisplay(event: CalendarEventBase) {
  return (
    getCalendarMatrixTestAssignmentCapacity(event) ?? {
      capacity: 0,
      assignedCount: 0,
      filledCount: 0,
      overflowCount: 0,
    }
  );
}

function getCapacitySlots(event: CalendarEventBase) {
  return Array.from({ length: getCapacityDisplay(event).capacity }, (_value, index) => index + 1);
}

function getVisibleAssignedUsers(event: CalendarEventBase) {
  return getCalendarMatrixTestAssignedUsers(event).slice(0, 2);
}

function getAssignedUserOverflowCount(event: CalendarEventBase) {
  return Math.max(getCalendarMatrixTestAssignedUsers(event).length - 2, 0);
}

function formatAssignedUserName(user: CalendarMatrixTestUser) {
  const parts = user.title.trim().split(/\s+/);
  const firstName = parts[0] ?? '';
  const lastName = parts.at(-1) ?? user.title;

  return firstName ? `${firstName.charAt(0)}. ${lastName}` : lastName;
}
</script>

<template>
  <CalendarMatrixView :model="model" :runtime-context="runtimeContext" @event-click="emit('eventClick', $event)">
    <template #cell-header="{ cell, header, onHeaderAction, onHeaderClick }">
      <div
        class="calendar-matrix-test-header"
        :class="{ 'has-conflict-overlay': calendarMatrixTestConflictEventId === header.id }"
      >
        <CalendarMatrixCellHeader :cell="cell" :header="header" @action="onHeaderAction" @click="onHeaderClick" />

        <section v-if="calendarMatrixTestConflictEventId === header.id" class="calendar-matrix-test-conflict-overlay">
          <div class="calendar-matrix-test-conflict-overlay__summary">
            <v-icon :icon="header.action?.icon" size="18" />
            <span>placeholder</span>
          </div>
          <button
            class="calendar-matrix-test-conflict-overlay__resolve"
            type="button"
            @click.stop="resolveHeaderConflict(cell, header, onHeaderAction)"
          >
            Resolve
          </button>
        </section>
      </div>
    </template>

    <template #event-block="{ event, display, group, onEventAction, onEventClick, onDragStart }">
      <div
        class="calendar-matrix-test-event-block"
        :class="{ 'has-conflict-overlay': calendarMatrixTestConflictEventId === event.id }"
      >
        <CalendarMatrixEventBlock
          :event="event"
          :display="display"
          :variant="group.variant"
          :show-color-bar="group.showColorBar"
          :time-zone="model.timeZone"
          @event-action="onEventAction"
          @drag-start="onDragStart"
          @event-click="onEventClick"
        >
          <template v-if="showAssignmentContent" #default="{ event }">
            <div class="calendar-matrix-test-assignment-block">
              <div
                class="calendar-matrix-test-capacity"
                :aria-label="`${getCapacityDisplay(event).assignedCount} of ${getCapacityDisplay(event).capacity} assignment slots filled`"
              >
                <span
                  v-for="slot in getCapacitySlots(event)"
                  :key="slot"
                  class="calendar-matrix-test-capacity__slot"
                  :class="{ 'is-filled': slot <= getCapacityDisplay(event).filledCount }"
                />
                <span v-if="getCapacityDisplay(event).overflowCount" class="calendar-matrix-test-capacity__overflow">
                  +{{ getCapacityDisplay(event).overflowCount }}
                </span>
              </div>

              <div v-if="getVisibleAssignedUsers(event).length" class="calendar-matrix-test-assigned-users">
                <span
                  v-for="user in getVisibleAssignedUsers(event)"
                  :key="user.id"
                  class="calendar-matrix-test-assigned-users__name"
                >
                  {{ formatAssignedUserName(user) }}
                </span>
                <span v-if="getAssignedUserOverflowCount(event)" class="calendar-matrix-test-assigned-users__overflow">
                  +{{ getAssignedUserOverflowCount(event) }} more
                </span>
              </div>
            </div>
          </template>
        </CalendarMatrixEventBlock>

        <section v-if="calendarMatrixTestConflictEventId === event.id" class="calendar-matrix-test-conflict-overlay">
          <div class="calendar-matrix-test-conflict-overlay__summary">
            <v-icon :icon="display?.action?.icon" size="18" />
            <span>placeholder</span>
          </div>
          <button
            class="calendar-matrix-test-conflict-overlay__resolve"
            type="button"
            @click.stop="resolveConflict(event, onEventAction)"
          >
            Resolve
          </button>
        </section>
      </div>
    </template>
  </CalendarMatrixView>

  <UaModal
    v-if="calendarMatrixTestDropModalState"
    title="Matrix Test Drop"
    width="560"
    @close="closeCalendarMatrixTestDropModal"
  >
    <section class="calendar-matrix-test-drop-modal">
      <div class="calendar-matrix-test-drop-modal__section">
        <h3 class="calendar-matrix-test-drop-modal__heading">Source</h3>
        <dl class="calendar-matrix-test-drop-modal__list">
          <template v-for="detail in sourceDetails" :key="detail.name">
            <dt>{{ detail.name }}</dt>
            <dd>{{ detail.value }}</dd>
          </template>
        </dl>
      </div>

      <div class="calendar-matrix-test-drop-modal__section">
        <h3 class="calendar-matrix-test-drop-modal__heading">Target</h3>
        <dl class="calendar-matrix-test-drop-modal__list">
          <template v-for="detail in targetDetails" :key="detail.name">
            <dt>{{ detail.name }}</dt>
            <dd>{{ detail.value }}</dd>
          </template>
        </dl>
      </div>
    </section>
  </UaModal>

  <UaModal
    v-if="calendarMatrixTestDetailEvent"
    title="Matrix Event Details"
    width="640"
    @close="closeCalendarMatrixTestEventDetail"
  >
    <dl class="calendar-matrix-test-detail-modal__list">
      <template v-for="detail in eventDetails" :key="detail.name">
        <dt>{{ detail.name }}</dt>
        <dd>{{ detail.value }}</dd>
      </template>
    </dl>
  </UaModal>

  <UaModal
    v-if="isCalendarMatrixTestAssignmentModalOpen"
    title="Add Assignment"
    width="520"
    @close="closeCalendarMatrixTestAssignmentModal"
  >
    <p class="calendar-matrix-test-assignment-modal__text">enter assignment details</p>
  </UaModal>

  <UaModal
    v-if="calendarMatrixTestResourceActionResource"
    title="Add Resource"
    width="560"
    @close="closeCalendarMatrixTestResourceActionModal"
  >
    <section class="calendar-matrix-test-resource-modal">
      <p class="calendar-matrix-test-resource-modal__text">add shift for resource:</p>
      <dl class="calendar-matrix-test-resource-modal__list">
        <template v-for="detail in resourceDetails" :key="detail.name">
          <dt>{{ detail.name }}</dt>
          <dd>{{ detail.value }}</dd>
        </template>
      </dl>
    </section>
  </UaModal>

  <UaModal
    v-if="calendarMatrixTestEventActionEvent"
    title="Event Action"
    width="640"
    @close="closeCalendarMatrixTestEventActionModal"
  >
    <section class="calendar-matrix-test-event-action-modal">
      <p class="calendar-matrix-test-event-action-modal__text">clicked add on event with details:</p>
      <dl class="calendar-matrix-test-event-action-modal__list">
        <template v-for="detail in eventActionDetails" :key="detail.name">
          <dt>{{ detail.name }}</dt>
          <dd>{{ detail.value }}</dd>
        </template>
      </dl>
    </section>
  </UaModal>
</template>

<style scoped>
.calendar-matrix-test-drop-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.calendar-matrix-test-drop-modal__section {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.calendar-matrix-test-drop-modal__heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.calendar-matrix-test-drop-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-matrix-test-drop-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-matrix-test-drop-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

.calendar-matrix-test-detail-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-matrix-test-detail-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-matrix-test-detail-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

.calendar-matrix-test-assignment-modal__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-matrix-test-resource-modal {
  display: grid;
  gap: var(--ua-spacing-md);
}

.calendar-matrix-test-resource-modal__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-matrix-test-resource-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-matrix-test-resource-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-matrix-test-resource-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

.calendar-matrix-test-event-action-modal {
  display: grid;
  gap: var(--ua-spacing-md);
}

.calendar-matrix-test-event-action-modal__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-matrix-test-event-action-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-matrix-test-event-action-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-matrix-test-event-action-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}

.calendar-matrix-test-event-block {
  position: relative;
}

.calendar-matrix-test-header {
  position: relative;
}

.calendar-matrix-test-header.has-conflict-overlay {
  z-index: 5;
}

.calendar-matrix-test-event-block.has-conflict-overlay {
  z-index: 5;
}

.calendar-matrix-test-assignment-block {
  display: grid;
  gap: var(--ua-spacing-sm);
  min-block-size: 3.25rem;
}

.calendar-matrix-test-capacity {
  align-items: start;
  display: flex;
  gap: 0.1875rem;
  min-block-size: 1rem;
}

.calendar-matrix-test-capacity__slot {
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-text-secondary);
  border-radius: 2px;
  display: inline-block;
  height: 0.625rem;
  width: 0.625rem;
}

.calendar-matrix-test-capacity__slot.is-filled {
  background: var(--calendar-event-border-color, var(--ua-text-secondary));
  border-color: var(--calendar-event-border-color, var(--ua-text-secondary));
}

.calendar-matrix-test-capacity__overflow {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-xs);
  font-weight: var(--ua-font-weight-bold);
  line-height: 1;
  margin-left: 0.125rem;
}

.calendar-matrix-test-assigned-users {
  align-self: end;
  color: var(--ua-text-secondary);
  display: flex;
  flex-wrap: wrap;
  font-size: var(--ua-font-size-xs);
  gap: 0.3625rem;
  line-height: 1.2;
}

.calendar-matrix-test-assigned-users::before {
  content: '- ';
}

.calendar-matrix-test-assigned-users__name,
.calendar-matrix-test-assigned-users__overflow {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.calendar-matrix-test-assigned-users__overflow {
  color: var(--ua-text-primary);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-matrix-test-conflict-overlay {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgb(var(--v-theme-error));
  border-radius: 4px;
  color: var(--ua-text-primary);
  display: grid;
  gap: var(--ua-spacing-sm);
  left: 0;
  padding: var(--ua-spacing-sm);
  position: absolute;
  right: 0;
  top: 1.875rem;
  z-index: 4;
}

.calendar-matrix-test-conflict-overlay__summary {
  align-items: center;
  color: rgb(var(--v-theme-error));
  display: flex;
  font-size: var(--ua-font-size-sm);
  gap: var(--ua-spacing-xs);
  line-height: 1.25;
}

.calendar-matrix-test-conflict-overlay__resolve {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgb(var(--v-theme-error));
  border-radius: 4px;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-sm);
  line-height: 1.25;
  padding: var(--ua-spacing-sm);
}
</style>
