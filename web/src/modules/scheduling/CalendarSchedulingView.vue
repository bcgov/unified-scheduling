<script setup lang="ts">
import CalendarSchedulingShiftDetailModal from './CalendarSchedulingShiftDetailModal.vue';
import CalendarSchedulingAddResourceModal from './CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingAssignmentModal from './CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingConflictOverlay from './CalendarSchedulingConflictOverlay.vue';
import CalendarConflictDetailModal from '@/modules/calendar/components/CalendarConflictDetailModal.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import CalendarMatrixCellHeader from '@/modules/calendar/components/matrix/CalendarMatrixCellHeader.vue';
import CalendarMatrixEventBlock from '@/modules/calendar/components/matrix/CalendarMatrixEventBlock.vue';
import CalendarMatrixView from '@/modules/calendar/components/matrix/CalendarMatrixView.vue';
import type {
  CalendarConflict,
  CalendarConflictEvent,
  CalendarEventBase,
  CalendarRuntimeContext,
} from '@/modules/calendar/calendarTypes';
import { postApiCalendarConflictsOverrides } from '@/api-access/generated/calendar/calendar';
import { Permissions } from '@/api-access/generated/models';
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
  calendarSchedulingAssignmentModalAssignmentDefinitionId,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalEditScope,
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalInitialTab,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalSeriesId,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingConflictEventId,
  calendarSchedulingDetailEvent,
  calendarSchedulingDetailInitialOpenScope,
  calendarSchedulingExistingShiftChoice,
  calendarSchedulingResourceActionAssignmentEntryId,
  calendarSchedulingResourceActionAssignmentEvents,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionResource,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingExistingShiftChoice,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingResourceActionModal,
  isCalendarSchedulingAssignmentModalOpen,
  isCalendarSchedulingResourceActionModalOpen,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
} from './calendarSchedulingState';
import { computed, ref } from 'vue';

const props = defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

const selectedConflict = ref<CalendarConflict>();
const selectedConflictEventId = ref<number>();
const conflictOverrideLoading = ref(false);
const conflictErrorMessage = ref('');
const canEditConflicts = computed(
  () => props.runtimeContext?.permissions?.includes(Permissions.AssignmentsEdit) === true,
);

function editExistingShift() {
  const choice = calendarSchedulingExistingShiftChoice.value;
  if (!choice) {
    return;
  }

  closeCalendarSchedulingExistingShiftChoice();
  showCalendarSchedulingEventDetail(choice.shiftEvent, { initialOpenScope: 'event' });
}

function createNewShift() {
  const choice = calendarSchedulingExistingShiftChoice.value;
  if (!choice) {
    return;
  }

  closeCalendarSchedulingExistingShiftChoice();
  showCalendarSchedulingResourceActionModal(choice.resource, choice.date, {
    assignmentEntryId: choice.assignmentEntryId,
    assignmentEvents: choice.assignmentEvents,
  });
}

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

function handleEventAction(
  payload: CalendarMatrixEventBlockActionEvent,
  conflicts: readonly CalendarConflict[],
  onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => void,
) {
  onEventAction(payload);
  if (payload.actionId === calendarSchedulingActionIds.showConflict) {
    showConflict(payload.event, conflicts);
  }
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

function showConflict(event: CalendarEventBase, conflicts: readonly CalendarConflict[]) {
  const eventId = resolveCalendarEventId(event);
  const conflict = conflicts.find(
    (candidate) => candidate.entry.eventId === eventId || candidate.overlaps.eventId === eventId,
  );
  if (conflict && eventId != null) {
    selectedConflict.value = conflict;
    selectedConflictEventId.value = eventId;
    conflictErrorMessage.value = '';
  }
}

async function overrideConflict(note: string) {
  const conflict = selectedConflict.value;
  const firstEventId = conflict?.entry.eventId;
  const secondEventId = conflict?.overlaps.eventId;
  if (!conflict || firstEventId == null || secondEventId == null) return;

  conflictOverrideLoading.value = true;
  conflictErrorMessage.value = '';
  const { error, execute } = postApiCalendarConflictsOverrides(
    {
      firstEventId,
      secondEventId,
      resourceId: conflict.resourceId,
      note,
    },
    { options: { immediate: false } },
  );
  await execute();
  conflictOverrideLoading.value = false;
  if (error.value) {
    conflictErrorMessage.value = error.value.message || 'Unable to override this conflict.';
    return;
  }
  selectedConflict.value = undefined;
  selectedConflictEventId.value = undefined;
}

function editConflictEvent(event: CalendarConflictEvent) {
  if (event.eventId == null) return;
  const scheduledEvent = findScheduledEvent(event.eventId);
  if (scheduledEvent) showCalendarSchedulingEventDetail(scheduledEvent);
}

function findScheduledEvent(eventId: number) {
  return props.model.cells
    .flatMap((cell) => [
      ...(cell.headers ?? []).map((header) => header.payload),
      ...cell.groups.flatMap((group) => group.events.map((item) => item.event)),
    ])
    .find((event): event is CalendarEventBase => isCalendarEvent(event) && resolveCalendarEventId(event) === eventId);
}

function resolveCalendarEventId(event: CalendarEventBase) {
  const value = (event as CalendarEventBase & { metadata?: { eventId?: unknown } }).metadata?.eventId;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function isCalendarEvent(value: unknown): value is CalendarEventBase {
  return typeof value === 'object' && value !== null && 'id' in value && 'start' in value;
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
          :event="header.payload as CalendarEventBase"
          :conflicts="header.conflicts ?? []"
          :icon="header.action?.icon"
          :time-zone="model.timeZone"
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
          @event-action="
            handleEventAction(
              $event,
              group.events.find((item) => item.event.id === event.id)?.conflicts ?? [],
              onEventAction,
            )
          "
          @drag-start="onDragStart"
          @event-click="onEventClick"
        />

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictEventId === event.id"
          :event="event"
          :conflicts="group.events.find((item) => item.event.id === event.id)?.conflicts ?? []"
          :icon="display?.action?.icon"
          :time-zone="model.timeZone"
          @resolve="resolveConflict(event, onEventAction)"
        />
      </div>
    </template>
  </CalendarMatrixView>

  <CalendarSchedulingShiftDetailModal
    v-if="calendarSchedulingDetailEvent"
    :event="calendarSchedulingDetailEvent"
    :initial-open-scope="calendarSchedulingDetailInitialOpenScope"
    @close="closeCalendarSchedulingEventDetail"
  />

  <CalendarConflictDetailModal
    v-if="selectedConflict && selectedConflictEventId != null"
    :conflict="selectedConflict"
    :current-event-id="selectedConflictEventId"
    :time-zone="model.timeZone"
    :loading="conflictOverrideLoading"
    :error-message="conflictErrorMessage"
    :can-edit-event="canEditConflicts"
    :can-override="canEditConflicts"
    @close="selectedConflict = undefined"
    @edit-event="editConflictEvent"
    @override="overrideConflict"
  />

  <CalendarSchedulingAssignmentModal
    v-if="isCalendarSchedulingAssignmentModalOpen"
    :mode="calendarSchedulingAssignmentModalMode"
    :initial-tab="calendarSchedulingAssignmentModalInitialTab"
    :edit-scope="calendarSchedulingAssignmentModalEditScope"
    :initial-date="calendarSchedulingAssignmentModalDate"
    :assignment-entry-id="calendarSchedulingAssignmentModalEntryId"
    :assignment-series-id="calendarSchedulingAssignmentModalSeriesId"
    :initial-assignment-definition-id="calendarSchedulingAssignmentModalAssignmentDefinitionId"
    :initial-shift-entry-ids="calendarSchedulingAssignmentModalShiftEntryIds"
    :time-zone="model.timeZone"
    @close="closeCalendarSchedulingAssignmentModal"
  />

  <UaModal
    v-if="calendarSchedulingExistingShiftChoice"
    title="Shift exists"
    width="520"
    @close="closeCalendarSchedulingExistingShiftChoice"
  >
    <p>This team member already has an active shift on this date. Would you like to edit it or create a new shift?</p>

    <template #actions>
      <UaBtn variant="outlined" @click="createNewShift">Create new shift</UaBtn>
      <UaBtn color="primary" variant="flat" @click="editExistingShift">Edit existing shift</UaBtn>
    </template>
  </UaModal>

  <CalendarSchedulingAddResourceModal
    v-if="isCalendarSchedulingResourceActionModalOpen"
    :initial-date="calendarSchedulingResourceActionDate"
    :initial-assignment-entry-id="calendarSchedulingResourceActionAssignmentEntryId"
    :initial-assignment-events="calendarSchedulingResourceActionAssignmentEvents"
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
