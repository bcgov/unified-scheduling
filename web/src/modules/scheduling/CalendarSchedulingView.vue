<script setup lang="ts">
import CalendarSchedulingShiftDetailModal from './CalendarSchedulingShiftDetailModal.vue';
import CalendarSchedulingAddResourceModal from './CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingConflictOverlay from './CalendarSchedulingConflictOverlay.vue';
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
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import {
  calendarSchedulingConflictEventId,
  calendarSchedulingDetailEvent,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionResource,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingResourceActionModal,
  isCalendarSchedulingResourceActionModalOpen,
} from './calendarSchedulingState';

defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

function resolveConflict(
  event: CalendarEventBase,
  onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => void,
) {
  onEventAction({
    event,
    actionId: calendarSchedulingActionIds.resolveConflict,
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
    actionId: calendarSchedulingActionIds.resolveConflict,
    actionType: CalendarMatrixActionType.Button,
  });
}
</script>

<template>
  <CalendarMatrixView :model="model" :runtime-context="runtimeContext" @event-click="emit('eventClick', $event)">
    <template #cell-header="{ cell, header, onHeaderAction, onHeaderClick }">
      <div
        class="calendar-scheduling-header"
        :class="{ 'has-conflict-overlay': calendarSchedulingConflictEventId === header.id }"
      >
        <CalendarMatrixCellHeader :cell="cell" :header="header" @action="onHeaderAction" @click="onHeaderClick" />

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictEventId === header.id"
          :icon="header.action?.icon"
          @resolve="resolveHeaderConflict(cell, header, onHeaderAction)"
        />
      </div>
    </template>

    <template #event-block="{ event, display, group, onEventAction, onEventClick, onDragStart }">
      <div
        class="calendar-scheduling-event-block"
        :class="{ 'has-conflict-overlay': calendarSchedulingConflictEventId === event.id }"
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
        />

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictEventId === event.id"
          :icon="display?.action?.icon"
          @resolve="resolveConflict(event, onEventAction)"
        />
      </div>
    </template>
  </CalendarMatrixView>

  <CalendarSchedulingShiftDetailModal
    v-if="calendarSchedulingDetailEvent"
    :event="calendarSchedulingDetailEvent"
    @close="closeCalendarSchedulingEventDetail"
  />

  <CalendarSchedulingAddResourceModal
    v-if="isCalendarSchedulingResourceActionModalOpen"
    :initial-date="calendarSchedulingResourceActionDate"
    :resource="calendarSchedulingResourceActionResource"
    :time-zone="model.timeZone"
    @close="closeCalendarSchedulingResourceActionModal"
  />
</template>

<style scoped>
.calendar-scheduling-event-block {
  position: relative;
}

.calendar-scheduling-header {
  position: relative;
}

.calendar-scheduling-header.has-conflict-overlay {
  z-index: 5;
}

.calendar-scheduling-event-block.has-conflict-overlay {
  z-index: 5;
}
</style>
