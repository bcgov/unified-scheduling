<script setup lang="ts">
import { computed, ref } from 'vue';
import { mdiRepeat } from '@mdi/js';
import { useRoute } from 'vue-router';
import CalendarSchedulingShiftDetailModal from './CalendarSchedulingShiftDetailModal.vue';
import CalendarSchedulingLeaveDetailModal from './CalendarSchedulingLeaveDetailModal.vue';
import CalendarSchedulingAddResourceModal from './CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingAssignmentModal from './CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingAssignmentEventContent from './CalendarSchedulingAssignmentEventContent.vue';
import CalendarSchedulingAssignmentDefinitionCreateModal from './CalendarSchedulingAssignmentDefinitionCreateModal.vue';
import CalendarSchedulingConflictOverlay from './CalendarSchedulingConflictOverlay.vue';
import CalendarSchedulingDistributionModal from './CalendarSchedulingDistributionModal.vue';
import CalendarSchedulingPublishModal from './CalendarSchedulingPublishModal.vue';
import CalendarConflictDetailModal from '@/modules/calendar/components/CalendarConflictDetailModal.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarPeriod } from '@/modules/calendar/calendarStore';
import { useCalendarAlertStore } from '@/modules/calendar/calendarAlertStore';
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
import { CalendarActionId } from '@/modules/calendar/calendarIdentifiers';
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
  calendarSchedulingAssignmentModalAssignedUserId,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalEditScope,
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalSeriesId,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingConflictEventId,
  calendarSchedulingConflictHeaderId,
  calendarSchedulingDistributionRequest,
  calendarSchedulingDetailEvent,
  calendarSchedulingDetailInitialOpenScope,
  calendarSchedulingExistingShiftChoice,
  calendarSchedulingResourceActionAssignmentEntryId,
  calendarSchedulingResourceActionAssignmentEvents,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionResource,
  calendarSchedulingLeaveDetailEvent,
  calendarSchedulingPublishRequest,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingExistingShiftChoice,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingDistributionModal,
  closeCalendarSchedulingPublishModal,
  closeCalendarSchedulingResourceActionModal,
  closeCalendarSchedulingLeaveDetail,
  getCalendarSchedulingHeaderConflictKey,
  isCalendarSchedulingAssignmentModalOpen,
  isCalendarSchedulingResourceActionModalOpen,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingEventDetail,
  showCalendarSchedulingResourceActionModal,
} from './calendarSchedulingState';
import { canCreateShifts, canEditShifts, canCreateLeave, canEditLeave } from './calendarSchedulingPermissions';
import { isCalendarSchedulingEvent, isLeaveEvent } from './calendarSchedulingData';

const props = defineProps<{
  model: CalendarMatrixViewModel;
  period?: CalendarPeriod;
  runtimeContext?: CalendarRuntimeContext;
  showAssignmentContent?: boolean;
  renderToken?: string;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'renderComplete', renderToken?: string): void;
}>();

const calendarStore = useCalendarStore();
const calendarAlertStore = useCalendarAlertStore();
const route = useRoute();
const assignmentDefinitionId = ref<number>();
const conflictEventCellKey = ref<string>();
const canCreateShift = computed(() => Boolean(props.runtimeContext && canCreateShifts(props.runtimeContext)));
const canEditShift = computed(() => Boolean(props.runtimeContext && canEditShifts(props.runtimeContext)));
const canAllowCreateLeave = computed(() => Boolean(props.runtimeContext && canCreateLeave(props.runtimeContext)));
const canAllowEditLeave = computed(() => Boolean(props.runtimeContext && canEditLeave(props.runtimeContext)));
const includeAssignmentTimes = computed(
  () =>
    props.runtimeContext?.presentationMode !== 'print' ||
    props.showAssignmentContent === true ||
    route.query.includeAssignmentTimes !== 'false',
);

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

function isRecurringLeave(event: CalendarEventBase) {
  return (
    isLeaveEvent(event) &&
    (event.eventSeriesId != null || (isCalendarSchedulingEvent(event) && event.metadata.leaveSeriesId != null))
  );
}

function handleSchedulePublished(count: number) {
  closeCalendarSchedulingPublishModal();
  calendarAlertStore.setAlert({
    id: 'calendar-scheduling.publish.success',
    severity: 'info',
    message: `${count} schedule item${count === 1 ? '' : 's'} published.`,
    source: 'calendar-scheduling',
  });
  calendarStore.refresh();
}

function editExistingShift() {
  if (!canEditShift.value && !canAllowEditLeave.value) {
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
  if (!canCreateShift.value && !canAllowCreateLeave.value) {
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
      firstSourceModule: conflict.entry.sourceModule,
      firstEventId: conflict.entry.eventId,
      secondSourceModule: conflict.overlaps.sourceModule,
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
  const eventId = parsePositiveInteger(event.eventId);
  if (!eventId) return;

  const scheduledEvent = findScheduledEvent(eventId);
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
    class="calendar-scheduling-matrix"
    :model="model"
    :render-token="renderToken"
    :runtime-context="runtimeContext"
    @event-click="emit('eventClick', $event)"
    @render-complete="emit('renderComplete', $event)"
    @side-panel-item-click="handleSidePanelItemClick"
  >
    <template #cell-header="{ cell, header, onHeaderAction, onHeaderClick }">
      <div
        class="calendar-scheduling-header"
        :class="{
          'is-holiday': header.actionId === CalendarActionId.EventDetail,
          'has-conflict-overlay':
            calendarSchedulingConflictHeaderId != null &&
            calendarSchedulingConflictHeaderId === getCalendarSchedulingHeaderConflictKey(cell, header.id),
        }"
      >
        <CalendarMatrixCellHeader :cell="cell" :header="header" @action="onHeaderAction" @click="onHeaderClick" />

        <CalendarSchedulingConflictOverlay
          v-if="
            calendarSchedulingConflictHeaderId != null &&
            calendarSchedulingConflictHeaderId === getCalendarSchedulingHeaderConflictKey(cell, header.id)
          "
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
          :show-time="includeAssignmentTimes || !isAssignmentEvent(event)"
          :time-zone="model.timeZone"
          @event-action="handleEventAction($event, cell, onEventAction)"
          @drag-start="onDragStart"
          @event-click="onEventClick"
        >
          <template #header>
            <span
              v-if="isRecurringLeave(event)"
              class="calendar-scheduling-event-block__recurrence"
              title="Recurring leave"
            >
              <v-icon :icon="mdiRepeat" size="small" aria-label="Recurring" />
            </span>
            {{ event.title }}
          </template>
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

  <CalendarSchedulingLeaveDetailModal
    v-if="calendarSchedulingLeaveDetailEvent"
    :event="calendarSchedulingLeaveDetailEvent"
    :runtime-context="runtimeContext"
    @close="closeCalendarSchedulingLeaveDetail"
  />

  <CalendarSchedulingAssignmentDefinitionCreateModal
    v-if="assignmentDefinitionId"
    :assignment-definition-id="assignmentDefinitionId"
    mode="view"
    @close="assignmentDefinitionId = undefined"
    @saved="handleAssignmentDefinitionSaved"
  />

  <CalendarSchedulingPublishModal
    v-if="calendarSchedulingPublishRequest"
    :request="calendarSchedulingPublishRequest"
    @close="closeCalendarSchedulingPublishModal"
    @published="handleSchedulePublished"
  />

  <CalendarSchedulingDistributionModal
    v-if="calendarSchedulingDistributionRequest"
    :location-id="calendarSchedulingDistributionRequest.locationId"
    :start-date="calendarSchedulingDistributionRequest.startDate"
    :end-date="calendarSchedulingDistributionRequest.endDate"
    :period="props.period ?? 'week'"
    :view="showAssignmentContent ? 'assignments' : 'schedule'"
    :model="model"
    @close="closeCalendarSchedulingDistributionModal"
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
    :initial-assigned-user-id="calendarSchedulingAssignmentModalAssignedUserId"
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
      <UaBtn v-if="canCreateShift" variant="outlined" @click="createNewShift"> Create new shift </UaBtn>
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
.calendar-scheduling-matrix {
  --calendar-matrix-scroll-padding-bottom: 10rem;
}

.calendar-scheduling-matrix :deep(.calendar-matrix-cell:has(.has-conflict-overlay)) {
  position: relative;
  z-index: 4;
}

.calendar-scheduling-event-block {
  position: relative;
}

.calendar-scheduling-header {
  position: relative;
}

.calendar-scheduling-header.is-holiday :deep(.calendar-matrix-cell-header) {
  background: rgb(var(--v-theme-surface));
  border-color: var(--calendar-event-holiday-color);
  color: var(--ua-text-primary);
}

.calendar-scheduling-header.has-conflict-overlay {
  z-index: 5;
}

.calendar-scheduling-event-block.has-conflict-overlay {
  z-index: 5;
}
</style>
