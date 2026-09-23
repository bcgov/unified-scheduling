<script setup lang="ts">
import { computed, ref } from 'vue';
import CalendarSchedulingShiftDetailModal from './CalendarSchedulingShiftDetailModal.vue';
import CalendarSchedulingAddResourceModal from './CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingAssignmentModal from './CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingAssignmentEventContent from './CalendarSchedulingAssignmentEventContent.vue';
import CalendarSchedulingAssignmentDefinitionCreateModal from './CalendarSchedulingAssignmentDefinitionCreateModal.vue';
import CalendarSchedulingConflictOverlay from './CalendarSchedulingConflictOverlay.vue';
import CalendarConflictDetailModal from '@/modules/calendar/components/CalendarConflictDetailModal.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { resolveCalendarEventId } from '@/modules/calendar/calendarSelectors';
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
  type CalendarMatrixConflictItem,
  type CalendarMatrixCell,
  type CalendarMatrixCellHeader as CalendarMatrixCellHeaderModel,
  type CalendarMatrixCellHeaderActionEvent,
  type CalendarMatrixEventBlockActionEvent,
  type CalendarMatrixSidePanelItem,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import { getCalendarEventDateKey } from './calendarSchedulingMappers';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import {
  calendarSchedulingAssignmentModalAssignmentDefinitionId,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalEditScope,
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalSeriesId,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingConflictEventId,
  calendarSchedulingConflictHeaderId,
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
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
} from './calendarSchedulingState';
import { canCreateShifts, canEditShifts } from './calendarSchedulingPermissions';

const props = defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
  showAssignmentContent?: boolean;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

const calendarStore = useCalendarStore();
const assignmentDefinitionId = ref<number>();
const conflictEventCellKey = ref<string>();
const canCreateShift = computed(() => Boolean(props.runtimeContext && canCreateShifts(props.runtimeContext)));
const canEditShift = computed(() => Boolean(props.runtimeContext && canEditShifts(props.runtimeContext)));

function handleSidePanelItemClick(item: CalendarMatrixSidePanelItem) {
  if (item.type !== 'assignment') {
    return;
  }

  const definitionId = Number(
    (item.payload as { assignmentDefinitionId?: unknown } | undefined)?.assignmentDefinitionId,
  );
  if (Number.isInteger(definitionId) && definitionId > 0) {
    assignmentDefinitionId.value = definitionId;
  }
}

function handleAssignmentDefinitionSaved() {
  assignmentDefinitionId.value = undefined;
  calendarStore.refresh();
}

function editExistingShift() {
  if (!canEditShift.value) {
    return;
  }

  const choice = calendarSchedulingExistingShiftChoice.value;
  if (!choice) {
    return;
  }

  closeCalendarSchedulingExistingShiftChoice();
  showCalendarSchedulingEventDetail(choice.shiftEvent, { initialOpenScope: 'event' });
}

function createNewShift() {
  if (!canCreateShift.value) {
    return;
  }

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

function eventCellKey(cell: CalendarMatrixCell, event: CalendarEventBase) {
  return `${cell.resourceId}:${cell.date}:${event.id}`;
}

function handleEventAction(
  payload: CalendarMatrixEventBlockActionEvent,
  cell: CalendarMatrixCell,
  onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => void,
) {
  if (payload.actionId === calendarSchedulingActionIds.showConflict) {
    const nextCellKey = eventCellKey(cell, payload.event);
    if (calendarSchedulingConflictEventId.value === payload.event.id && conflictEventCellKey.value !== nextCellKey) {
      calendarSchedulingConflictEventId.value = undefined;
    }
    conflictEventCellKey.value = nextCellKey;
  } else if (payload.actionId === calendarSchedulingActionIds.resolveConflict) {
    conflictEventCellKey.value = undefined;
  }

  onEventAction(payload);
}

const selectedConflict = ref<CalendarConflict>();
const selectedConflictEventId = ref<number>();
const conflictOverrideLoading = ref(false);
const conflictErrorMessage = ref('');
const canEditConflictEvents = computed(
  () =>
    selectedConflict.value != null &&
    selectedConflict.value.entry.sourceModule === 'scheduling' &&
    selectedConflict.value.overlaps.sourceModule === 'scheduling' &&
    props.runtimeContext?.permissions?.includes(Permissions.AssignmentsEdit) === true,
);
const canOverrideConflicts = computed(
  () => props.runtimeContext?.permissions?.includes(Permissions.CalendarConflictsOverride) === true,
);
function resolveConflict(
  conflictItem: CalendarMatrixConflictItem,
  event: CalendarEventBase,
  onEventAction: (payload: CalendarMatrixEventBlockActionEvent) => void,
) {
  onEventAction({
    event,
    actionId: calendarSchedulingActionIds.resolveConflict,
    actionType: CalendarMatrixActionType.Button,
  });
  showConflict(conflictItem);
}

function resolveHeaderConflict(
  conflictItem: CalendarMatrixConflictItem,
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
  showConflict(conflictItem);
}

function showConflict({ conflict, currentEventId }: CalendarMatrixConflictItem) {
  selectedConflict.value = conflict;
  selectedConflictEventId.value = currentEventId;
  conflictErrorMessage.value = '';
}

async function overrideConflict(note: string) {
  const conflict = selectedConflict.value;
  if (!conflict) return;

  conflictOverrideLoading.value = true;
  conflictErrorMessage.value = '';
  const { error, execute } = postApiCalendarConflictsOverrides(
    {
      firstEventId: conflict.entry.eventId,
      secondEventId: conflict.overlaps.eventId,
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
  calendarStore.refresh();
}

function editConflictEvent(event: CalendarConflictEvent) {
  const scheduledEvent = findScheduledEvent(event.eventId);
  if (!scheduledEvent) return;

  if (isAssignmentEvent(scheduledEvent)) {
    const assignmentEntryId = parsePositiveInteger(
      (scheduledEvent as { metadata?: { assignmentEntryId?: unknown } }).metadata?.assignmentEntryId,
    );
    const eventDate = getCalendarEventDateKey(scheduledEvent.start, scheduledEvent.timeZoneId ?? props.model.timeZone);
    if (!assignmentEntryId || !eventDate) return;

    selectedConflict.value = undefined;
    selectedConflictEventId.value = undefined;
    showCalendarSchedulingAssignmentModal(eventDate, {
      mode: 'edit',
      editScope: 'event',
      assignmentEntryId,
      assignmentSeriesId:
        parsePositiveInteger(
          (scheduledEvent as { metadata?: { assignmentSeriesId?: unknown } }).metadata?.assignmentSeriesId,
        ) ?? undefined,
    });
    return;
  }

  selectedConflict.value = undefined;
  selectedConflictEventId.value = undefined;
  showCalendarSchedulingEventDetail(scheduledEvent);
}

function findScheduledEvent(eventId: number) {
  return props.model.cells
    .flatMap((cell) => [
      ...(cell.headers ?? []).map((header) => header.payload),
      ...cell.groups.flatMap((group) => group.events.map((item) => item.event)),
    ])
    .find((event): event is CalendarEventBase => isCalendarEvent(event) && resolveCalendarEventId(event) === eventId);
}

function isCalendarEvent(value: unknown): value is CalendarEventBase {
  return typeof value === 'object' && value !== null && 'id' in value && 'start' in value;
}

function isAssignmentEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.assignment' || event.eventTypeCode === 'assignment';
}
</script>

<template>
  <CalendarMatrixView
    :model="model"
    :runtime-context="runtimeContext"
    @event-click="emit('eventClick', $event)"
    @side-panel-item-click="handleSidePanelItemClick"
  >
    <template #cell-header="{ cell, header, onHeaderAction, onHeaderClick }">
      <div
        class="calendar-scheduling-header"
        :class="{ 'has-conflict-overlay': calendarSchedulingConflictHeaderId === header.id }"
      >
        <CalendarMatrixCellHeader :cell="cell" :header="header" @action="onHeaderAction" @click="onHeaderClick" />

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictHeaderId === header.id"
          :conflicts="header.conflicts ?? []"
          :icon="header.action?.icon"
          :time-zone="model.timeZone"
          @resolve="resolveHeaderConflict($event, cell, header, onHeaderAction)"
        />
      </div>
    </template>

    <template #event-block="{ cell, event, display, group, onEventAction, onEventClick, onDragStart }">
      <div
        class="calendar-scheduling-event-block"
        :class="{
          'has-conflict-overlay':
            calendarSchedulingConflictEventId === event.id && conflictEventCellKey === eventCellKey(cell, event),
        }"
      >
        <CalendarMatrixEventBlock
          :event="event"
          :display="display"
          :variant="group.variant"
          :show-color-bar="group.showColorBar"
          :time-zone="model.timeZone"
          @event-action="handleEventAction($event, cell, onEventAction)"
          @drag-start="onDragStart"
          @event-click="onEventClick"
        >
          <CalendarSchedulingAssignmentEventContent v-if="showAssignmentContent" :event="event" />
        </CalendarMatrixEventBlock>

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictEventId === event.id && conflictEventCellKey === eventCellKey(cell, event)"
          :conflicts="group.events.find((item) => item.event === event)?.conflicts ?? []"
          :icon="display?.action?.icon"
          :time-zone="model.timeZone"
          @resolve="resolveConflict($event, event, onEventAction)"
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

  <CalendarSchedulingAssignmentDefinitionCreateModal
    v-if="assignmentDefinitionId"
    :assignment-definition-id="assignmentDefinitionId"
    mode="view"
    @close="assignmentDefinitionId = undefined"
    @saved="handleAssignmentDefinitionSaved"
  />

  <CalendarSchedulingAssignmentModal
    v-if="isCalendarSchedulingAssignmentModalOpen"
    :mode="calendarSchedulingAssignmentModalMode"
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
      <UaBtn v-if="canCreateShift" variant="outlined" @click="createNewShift">Create new shift</UaBtn>
      <UaBtn v-if="canEditShift" color="primary" variant="flat" @click="editExistingShift">Edit existing shift</UaBtn>
    </template>
  </UaModal>

  <CalendarConflictDetailModal
    v-if="selectedConflict && selectedConflictEventId != null"
    :conflict="selectedConflict"
    :current-event-id="selectedConflictEventId"
    :time-zone="model.timeZone"
    :loading="conflictOverrideLoading"
    :error-message="conflictErrorMessage"
    :can-edit-event="canEditConflictEvents"
    :can-override="canOverrideConflicts"
    @close="selectedConflict = undefined"
    @edit-event="editConflictEvent"
    @override="overrideConflict"
  />
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
