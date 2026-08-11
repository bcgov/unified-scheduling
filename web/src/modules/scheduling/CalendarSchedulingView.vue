<script setup lang="ts">
import CalendarSchedulingShiftDetailModal from './CalendarSchedulingShiftDetailModal.vue';
import CalendarSchedulingAddResourceModal from './CalendarSchedulingAddResourceModal.vue';
import CalendarSchedulingAssignmentDefinitionCreateModal from './CalendarSchedulingAssignmentDefinitionCreateModal.vue';
import CalendarSchedulingAssignmentEventContent from './CalendarSchedulingAssignmentEventContent.vue';
import CalendarSchedulingAssignmentModal from './CalendarSchedulingAssignmentModal.vue';
import CalendarSchedulingConflictOverlay from './CalendarSchedulingConflictOverlay.vue';
import CalendarConflictDetailModal from '@/modules/calendar/components/CalendarConflictDetailModal.vue';
import CalendarMatrixCellHeader from '@/modules/calendar/components/matrix/CalendarMatrixCellHeader.vue';
import CalendarMatrixEventBlock from '@/modules/calendar/components/matrix/CalendarMatrixEventBlock.vue';
import CalendarMatrixView from '@/modules/calendar/components/matrix/CalendarMatrixView.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { formatCalendarEventDate, formatCalendarEventTimeRange } from '@/utils/date';
import type { CalendarConflict, CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import { postApiCalendarConflictOverride } from '@/api-access/calendar';
import {
  type CalendarMatrixSidePanelItem,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import {
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalEditScope,
  calendarSchedulingAssignmentModalDate,
  calendarSchedulingAssignmentModalInitialTab,
  calendarSchedulingAssignmentModalAssignmentDefinitionId,
  calendarSchedulingAssignmentModalExistingEvents,
  calendarSchedulingAssignmentModalMode,
  calendarSchedulingAssignmentModalSeriesId,
  calendarSchedulingAssignmentModalShiftEntryIds,
  calendarSchedulingConflictEventId,
  calendarSchedulingDetailEvent,
  calendarSchedulingResourceActionDate,
  calendarSchedulingResourceActionAssignmentEvents,
  calendarSchedulingResourceActionAssignmentEntryId,
  calendarSchedulingResourceActionResource,
  closeCalendarSchedulingConflict,
  closeCalendarSchedulingEventDetail,
  closeCalendarSchedulingAssignmentModal,
  closeCalendarSchedulingResourceActionModal,
  isCalendarSchedulingAssignmentModalOpen,
  isCalendarSchedulingResourceActionModalOpen,
  showCalendarSchedulingResourceActionModal,
  showCalendarSchedulingAssignmentModal,
  showCalendarSchedulingEventDetail,
} from './calendarSchedulingState';
import { isCalendarSchedulingEvent, type CalendarUser } from './calendarSchedulingData';
import type {
  CalendarMatrixDragPayload,
  CalendarMatrixResource,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { computed, ref } from 'vue';

const props = defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
  showAssignmentContent?: boolean;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

const calendarStore = useCalendarStore();
const pendingAssignmentSeriesEvent = ref<CalendarEventBase>();
const selectedAssignmentDefinitionId = ref<number>();
const selectedConflictEventId = ref<number>();
const selectedConflict = ref<CalendarConflict>();
const conflictOverrideLoading = ref(false);
const conflictErrorMessage = ref('');
const pendingActiveShiftChoice = ref<{
  shiftEvent: CalendarEventBase;
  resource: CalendarMatrixResource;
  date: string;
  assignmentEntryId: number;
  assignmentEvent: CalendarEventBase;
}>();
const currentCalendarRangeStartDate = computed(() => calendarStore.dateRange.startDate || props.model.days[0]?.date);
const assignmentModalInitialDate = computed(() =>
  calendarSchedulingAssignmentModalMode.value === 'create'
    ? (calendarSchedulingAssignmentModalDate.value ?? currentCalendarRangeStartDate.value)
    : calendarSchedulingAssignmentModalDate.value,
);
const resourceActionModalInitialDate = computed(
  () => calendarSchedulingResourceActionDate.value ?? currentCalendarRangeStartDate.value,
);
const assignmentContentUsers = computed<CalendarUser[]>(() => {
  const users = new Map<string, CalendarUser>();

  for (const resource of props.model.primaryColumn.resources) {
    if (resource.type === 'user') {
      users.set(resource.id, {
        id: resource.id,
        type: 'user',
        title: resource.title,
        subtitle: resource.subtitle,
        meta: resource.meta,
        avatarText: resource.avatarText,
      });
    }
  }

  for (const item of props.model.sidePanel?.items ?? []) {
    if (item.type === 'user') {
      users.set(item.id, {
        id: item.id,
        type: 'user',
        title: item.title,
        subtitle: item.subtitle,
        meta: item.meta,
        avatarText: item.avatarText,
      });
    }
  }

  return Array.from(users.values());
});

function isAssignmentSidePanelItem(item: CalendarMatrixSidePanelItem) {
  return item.type === 'assignment';
}

function openAssignmentDefinition(item: CalendarMatrixSidePanelItem) {
  const assignmentDefinitionId = resolveAssignmentDefinitionIdFromItem(item);
  if (assignmentDefinitionId) {
    selectedAssignmentDefinitionId.value = assignmentDefinitionId;
  }
}

function closeAssignmentDefinition() {
  selectedAssignmentDefinitionId.value = undefined;
}

function handleAssignmentDefinitionSaved() {
  calendarStore.refresh();
  closeAssignmentDefinition();
}

function resolveAssignmentDefinitionIdFromItem(item: CalendarMatrixSidePanelItem) {
  const payload = item.payload;
  if (typeof payload !== 'object' || payload === null) {
    return null;
  }

  const parsed = Number((payload as { assignmentDefinitionId?: unknown }).assignmentDefinitionId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function getAssignmentTimeRange(item: CalendarMatrixSidePanelItem) {
  const payload = item.payload as { defaultStartTime?: string; defaultEndTime?: string } | undefined;
  const start = formatDefaultTime(payload?.defaultStartTime);
  const end = formatDefaultTime(payload?.defaultEndTime);

  if (start && end) {
    return `${start}-${end}`;
  }

  return start || end || '';
}

function getAssignmentCapacity(item: CalendarMatrixSidePanelItem) {
  const payload = item.payload as { capacity?: number } | undefined;
  const capacity = Number(payload?.capacity ?? 0);
  return Number.isFinite(capacity) && capacity > 0 ? Math.floor(capacity) : 0;
}

function formatDefaultTime(value?: string) {
  if (!value) {
    return '';
  }

  const [hourPart, minutePart = '0'] = value.split(':');
  const hour = Number(hourPart);
  const minute = Number(minutePart);

  if (!Number.isFinite(hour) || !Number.isFinite(minute)) {
    return '';
  }

  const suffix = hour >= 12 ? 'pm' : 'am';
  const twelveHour = hour % 12 || 12;
  const minuteText = minute > 0 ? `:${String(minute).padStart(2, '0')}` : '';
  return `${twelveHour}${minuteText}${suffix}`;
}

async function handleAssignmentEventDrop(event: DragEvent, targetEvent: CalendarEventBase) {
  const drag = readDragPayload(event);
  const assignmentEntryId = resolveAssignmentEntryId(targetEvent);

  if (!drag) {
    return;
  }

  if (drag.itemType === 'assignment') {
    const assignmentDefinitionId = resolveAssignmentDefinitionId(drag);
    const shiftEntryId = resolveShiftEntryId(targetEvent);

    if (!assignmentDefinitionId || !shiftEntryId) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    showCalendarSchedulingAssignmentModal(getEventDate(targetEvent), {
      assignmentDefinitionId,
      shiftEntryIds: [shiftEntryId],
    });
    return;
  }

  if (drag.itemType === 'user' && assignmentEntryId) {
    event.preventDefault();
    event.stopPropagation();

    const resource = toUserResource(drag);
    const date = getEventDate(targetEvent);
    const activeShift = findExistingShiftEventOnDate(date);

    if (activeShift) {
      pendingActiveShiftChoice.value = {
        shiftEvent: activeShift,
        resource,
        date,
        assignmentEntryId,
        assignmentEvent: targetEvent,
      };
      return;
    }

    showCalendarSchedulingResourceActionModal(resource, date, {
      assignmentEntryId,
      assignmentEvents: [targetEvent],
    });
  }
}

function readDragPayload(event: DragEvent): CalendarMatrixDragPayload | null {
  const raw = event.dataTransfer?.getData('application/json');

  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as CalendarMatrixDragPayload;
  } catch {
    return null;
  }
}

function resolveAssignmentEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  const parsed = Number(event.metadata.assignmentEntryId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function resolveAssignmentSeriesId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  const parsed = Number(event.metadata.assignmentSeriesId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function isAssignmentEvent(event: CalendarEventBase) {
  return (
    event.type === 'scheduling.assignment' ||
    event.eventTypeCode === 'assignment' ||
    Boolean(resolveAssignmentEntryId(event))
  );
}

function resolveShiftEntryId(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return null;
  }

  const parsed = Number(event.metadata.shiftEntryId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function findExistingShiftEventOnDate(date: string) {
  for (const cell of props.model.cells) {
    if (cell.date !== date) {
      continue;
    }

    for (const event of getPayloadShiftEvents(cell.payload)) {
      if (isShiftEvent(event) && !isCancelledStatus(event.statusTypeCode)) {
        return event;
      }
    }

    for (const group of cell.groups) {
      for (const item of group.events) {
        if (isShiftEvent(item.event) && !isCancelledStatus(item.event.statusTypeCode)) {
          return item.event;
        }
      }
    }
  }

  return null;
}

function getPayloadShiftEvents(payload: unknown): CalendarEventBase[] {
  if (typeof payload !== 'object' || payload === null) {
    return [];
  }

  const shiftEvents = (payload as { shiftEvents?: unknown }).shiftEvents;
  return Array.isArray(shiftEvents) ? shiftEvents.filter(isCalendarEventBase) : [];
}

function isCalendarEventBase(value: unknown): value is CalendarEventBase {
  return (
    typeof value === 'object' &&
    value !== null &&
    typeof (value as { id?: unknown }).id === 'string' &&
    typeof (value as { start?: unknown }).start === 'string'
  );
}

function isShiftEvent(event: CalendarEventBase) {
  return event.type === 'scheduling.shift' || event.eventTypeCode === 'shift' || Boolean(resolveShiftEntryId(event));
}

function isCancelledStatus(status?: string | null) {
  return String(status ?? '')
    .toLowerCase()
    .includes('cancel');
}

function closeActiveShiftChoice() {
  pendingActiveShiftChoice.value = undefined;
}

function editExistingActiveShift() {
  const choice = pendingActiveShiftChoice.value;
  if (!choice) {
    return;
  }

  pendingActiveShiftChoice.value = undefined;
  showCalendarSchedulingEventDetail(choice.shiftEvent);
}

function createNewShiftDespiteExistingActiveShift() {
  const choice = pendingActiveShiftChoice.value;
  if (!choice) {
    return;
  }

  pendingActiveShiftChoice.value = undefined;
  showCalendarSchedulingResourceActionModal(choice.resource, choice.date, {
    assignmentEntryId: choice.assignmentEntryId,
    assignmentEvents: [choice.assignmentEvent],
  });
}

function formatActiveShiftChoiceDetails() {
  const shift = pendingActiveShiftChoice.value?.shiftEvent;
  if (!shift) {
    return '';
  }

  const date = formatCalendarEventDate(shift.start, {
    allDay: shift.allDay,
    timeZone: shift.timeZoneId || props.model.timeZone,
  });
  const time = formatCalendarEventTimeRange(shift.start, shift.end, {
    allDay: shift.allDay,
    timeZone: shift.timeZoneId || props.model.timeZone,
  });
  const users = shift.resourceIds?.length
    ? `Users: ${shift.resourceIds.map(formatUserTitle).join(', ')}`
    : 'Users: None';

  return [shift.title, date, time, users].filter(Boolean).join('\n');
}

function formatUserTitle(userId: string) {
  const user = assignmentContentUsers.value.find((candidate) => candidate.id === userId);
  return user?.title || userId;
}

function resolveAssignmentDefinitionId(drag: CalendarMatrixDragPayload) {
  if (typeof drag.payload !== 'object' || drag.payload === null) {
    return null;
  }

  const parsed = Number((drag.payload as { assignmentDefinitionId?: unknown }).assignmentDefinitionId);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function handleEventClick(event: CalendarEventBase) {
  const assignmentEntryId = resolveAssignmentEntryId(event);
  const assignmentSeriesId = resolveAssignmentSeriesId(event);

  if (isAssignmentEvent(event) && assignmentEntryId) {
    if (assignmentSeriesId) {
      pendingAssignmentSeriesEvent.value = event;
      return;
    }

    showCalendarSchedulingAssignmentModal(getEventDate(event), {
      mode: 'view',
      editScope: 'event',
      assignmentEntryId,
    });
    return;
  }

  emit('eventClick', event);
}

function openAssignmentScope(scope: 'event' | 'series') {
  const event = pendingAssignmentSeriesEvent.value;
  const assignmentEntryId = event ? resolveAssignmentEntryId(event) : null;
  const assignmentSeriesId = event ? resolveAssignmentSeriesId(event) : null;

  pendingAssignmentSeriesEvent.value = undefined;

  if (!event) {
    return;
  }

  if (scope === 'series' && assignmentSeriesId) {
    showCalendarSchedulingAssignmentModal(getEventDate(event), {
      mode: 'view',
      editScope: 'series',
      assignmentEntryId: assignmentEntryId ?? undefined,
      assignmentSeriesId,
    });
    return;
  }

  if (assignmentEntryId) {
    showCalendarSchedulingAssignmentModal(getEventDate(event), {
      mode: 'view',
      editScope: 'event',
      assignmentEntryId,
    });
  }
}

function closeAssignmentScopeChoice() {
  pendingAssignmentSeriesEvent.value = undefined;
}

function handleMatrixEventBlockClick(event: CalendarEventBase, onEventClick: (event: CalendarEventBase) => void) {
  if (isAssignmentEvent(event)) {
    handleEventClick(event);
    return;
  }

  onEventClick(event);
}

function toUserResource(drag: CalendarMatrixDragPayload): CalendarMatrixResource {
  return {
    id: drag.itemId,
    type: 'user',
    title: resolveDragTitle(drag),
  };
}

function resolveDragTitle(drag: CalendarMatrixDragPayload) {
  if (typeof drag.payload === 'object' && drag.payload !== null) {
    const title = (drag.payload as { title?: unknown; userId?: unknown }).title;
    if (typeof title === 'string' && title.trim()) {
      return title;
    }
  }

  return drag.itemId;
}

function getEventDate(event: CalendarEventBase) {
  return event.start.slice(0, 10);
}

function handleConflictResolve(event: CalendarEventBase, conflict: CalendarConflict) {
  if (!isCalendarSchedulingEvent(event) || !event.metadata.eventId) {
    return;
  }

  closeCalendarSchedulingConflict();
  conflictErrorMessage.value = '';
  selectedConflictEventId.value = event.metadata.eventId;
  selectedConflict.value = conflict;
}

function closeConflictDetail() {
  selectedConflictEventId.value = undefined;
  selectedConflict.value = undefined;
  conflictErrorMessage.value = '';
}

function handleConflictEventEdit(eventId: number) {
  const event = resolveAssignmentEventsFromModel().find(
    (candidate) => isCalendarSchedulingEvent(candidate) && candidate.metadata.eventId === eventId,
  );
  const assignmentEntryId = event ? resolveAssignmentEntryId(event) : null;
  if (!event || !assignmentEntryId) {
    return;
  }

  closeConflictDetail();
  showCalendarSchedulingAssignmentModal(getEventDate(event), {
    mode: 'view',
    initialTab: 'edit',
    editScope: 'event',
    assignmentEntryId,
  });
}

async function handleConflictOverride(note: string) {
  const conflict = selectedConflict.value;
  if (!conflict) {
    return;
  }

  conflictOverrideLoading.value = true;
  conflictErrorMessage.value = '';
  try {
    const savedOverride = await postApiCalendarConflictOverride({
      firstEventId: conflict.entry.eventId,
      secondEventId: conflict.overlaps.eventId,
      note,
    });
    selectedConflict.value = {
      ...conflict,
      isOverridden: true,
      overrideId: savedOverride.id,
      overrideNote: savedOverride.note,
      createdById: savedOverride.createdById,
      createdOn: savedOverride.createdOn,
      updatedById: savedOverride.updatedById,
      updatedOn: savedOverride.updatedOn,
    };
    calendarStore.refresh();
  } catch {
    conflictErrorMessage.value = 'Failed to save the conflict override.';
  } finally {
    conflictOverrideLoading.value = false;
  }
}

function resolveAssignmentEventsFromModel() {
  const payload = props.model.payload;
  if (typeof payload === 'object' && payload !== null) {
    const assignmentEvents = (payload as { assignmentEvents?: unknown }).assignmentEvents;
    if (Array.isArray(assignmentEvents)) {
      return assignmentEvents.filter(isCalendarEventBase);
    }
  }

  return props.model.cells.flatMap((cell) =>
    cell.groups.flatMap((group) => group.events.map((item) => item.event).filter(isAssignmentEvent)),
  );
}
</script>

<template>
  <CalendarMatrixView :model="props.model" :runtime-context="props.runtimeContext" @event-click="handleEventClick">
    <template #cell-header="{ cell, header, onHeaderAction, onHeaderClick }">
      <div class="calendar-scheduling-header">
        <CalendarMatrixCellHeader :cell="cell" :header="header" @action="onHeaderAction" @click="onHeaderClick" />
      </div>
    </template>

    <template #event-block="{ event, display, group, onEventAction, onEventClick, onDragStart }">
      <div
        class="calendar-scheduling-event-block"
        :class="{ 'has-conflict-overlay': calendarSchedulingConflictEventId === event.id }"
        @dragover.prevent
        @drop="(dragEvent) => handleAssignmentEventDrop(dragEvent, event)"
      >
        <CalendarMatrixEventBlock
          :event="event"
          :display="display"
          :variant="group.variant"
          :show-color-bar="group.showColorBar"
          :select-on-click="!isAssignmentEvent(event)"
          :time-zone="props.model.timeZone"
          @event-action="onEventAction"
          @drag-start="onDragStart"
          @event-click="(clickedEvent) => handleMatrixEventBlockClick(clickedEvent, onEventClick)"
        >
          <template v-if="props.showAssignmentContent" #default="{ event }">
            <CalendarSchedulingAssignmentEventContent :event="event" :users="assignmentContentUsers" />
          </template>
        </CalendarMatrixEventBlock>

        <CalendarSchedulingConflictOverlay
          v-if="calendarSchedulingConflictEventId === event.id"
          :event="event"
          :icon="display?.action?.icon"
          :time-zone="props.model.timeZone"
          @resolve="(conflict) => handleConflictResolve(event, conflict)"
        />
      </div>
    </template>

    <template #side-panel-item="{ item }">
      <div
        v-if="isAssignmentSidePanelItem(item)"
        class="calendar-scheduling-assignment-definition"
        role="button"
        tabindex="0"
        @click="openAssignmentDefinition(item)"
        @keydown.enter.prevent="openAssignmentDefinition(item)"
        @keydown.space.prevent="openAssignmentDefinition(item)"
      >
        <div class="calendar-scheduling-assignment-definition__content">
          <strong class="calendar-scheduling-assignment-definition__name">{{ item.title }}</strong>
          <span v-if="getAssignmentTimeRange(item)" class="calendar-scheduling-assignment-definition__time">
            {{ getAssignmentTimeRange(item) }}
          </span>
          <span v-if="item.subtitle" class="calendar-scheduling-assignment-definition__subtitle">
            {{ item.subtitle }}
          </span>
          <span
            v-if="getAssignmentCapacity(item)"
            class="calendar-scheduling-assignment-definition__slots"
            :aria-label="`${getAssignmentCapacity(item)} Capacity Slots`"
          >
            <span
              v-for="slot in getAssignmentCapacity(item)"
              :key="slot"
              class="calendar-scheduling-assignment-definition__slot"
            ></span>
          </span>
        </div>
      </div>
      <template v-else>
        <div class="calendar-scheduling-team-member">
          <span v-if="item.avatarText" class="calendar-scheduling-team-member__avatar">{{ item.avatarText }}</span>
          <div class="calendar-scheduling-team-member__content">
            <strong class="calendar-scheduling-team-member__name">{{ item.title }}</strong>
            <span v-if="item.subtitle" class="calendar-scheduling-team-member__position">{{ item.subtitle }}</span>
            <span v-if="item.meta?.length" class="calendar-scheduling-team-member__meta-list">
              <span
                v-for="(metaItem, index) in item.meta"
                :key="`${metaItem.label ?? 'value'}-${metaItem.value}-${index}`"
                class="calendar-scheduling-team-member__meta"
              >
                <span v-if="metaItem.label" class="calendar-scheduling-team-member__meta-label">
                  {{ metaItem.label }}:
                </span>
                {{ metaItem.value }}
              </span>
            </span>
          </div>
        </div>
      </template>
    </template>
  </CalendarMatrixView>

  <CalendarSchedulingShiftDetailModal
    v-if="calendarSchedulingDetailEvent"
    :event="calendarSchedulingDetailEvent"
    @close="closeCalendarSchedulingEventDetail"
  />

  <CalendarSchedulingAssignmentModal
    v-if="isCalendarSchedulingAssignmentModalOpen"
    :initial-date="assignmentModalInitialDate"
    :mode="calendarSchedulingAssignmentModalMode"
    :initial-tab="calendarSchedulingAssignmentModalInitialTab"
    :edit-scope="calendarSchedulingAssignmentModalEditScope"
    :assignment-entry-id="calendarSchedulingAssignmentModalEntryId"
    :assignment-series-id="calendarSchedulingAssignmentModalSeriesId"
    :initial-assignment-definition-id="calendarSchedulingAssignmentModalAssignmentDefinitionId"
    :initial-shift-entry-ids="calendarSchedulingAssignmentModalShiftEntryIds"
    :existing-assignment-events="calendarSchedulingAssignmentModalExistingEvents"
    :time-zone="props.model.timeZone"
    @close="closeCalendarSchedulingAssignmentModal"
  />

  <CalendarSchedulingAssignmentDefinitionCreateModal
    v-if="selectedAssignmentDefinitionId"
    mode="view"
    :assignment-definition-id="selectedAssignmentDefinitionId"
    @close="closeAssignmentDefinition"
    @saved="handleAssignmentDefinitionSaved"
  />

  <UaModal v-if="pendingAssignmentSeriesEvent" title="Open Assignment" width="420" @close="closeAssignmentScopeChoice">
    <p class="calendar-scheduling-scope-choice__text">This is one event in a series. What do you want to open?</p>

    <template #actions>
      <UaBtn variant="outlined" @click="openAssignmentScope('event')">Only This Event</UaBtn>
      <UaBtn color="primary" variant="flat" @click="openAssignmentScope('series')">The Entire Series</UaBtn>
    </template>
  </UaModal>

  <UaModal v-if="pendingActiveShiftChoice" title="Shift Exists" width="520" @close="closeActiveShiftChoice">
    <div class="calendar-scheduling-active-shift-choice">
      <p class="calendar-scheduling-active-shift-choice__text">There is already a shift at this location:</p>
      <pre class="calendar-scheduling-active-shift-choice__details">{{ formatActiveShiftChoiceDetails() }}</pre>
      <p class="calendar-scheduling-active-shift-choice__text">
        Would you like to edit the existing shift, or create a new shift?
      </p>
    </div>

    <template #actions>
      <UaBtn variant="outlined" @click="createNewShiftDespiteExistingActiveShift">Create New Shift</UaBtn>
      <UaBtn color="primary" variant="flat" @click="editExistingActiveShift">Edit Existing Shift</UaBtn>
    </template>
  </UaModal>

  <CalendarSchedulingAddResourceModal
    v-if="isCalendarSchedulingResourceActionModalOpen"
    :initial-date="resourceActionModalInitialDate"
    :initial-assignment-entry-id="calendarSchedulingResourceActionAssignmentEntryId"
    :initial-assignment-events="calendarSchedulingResourceActionAssignmentEvents"
    :resource="calendarSchedulingResourceActionResource"
    :time-zone="props.model.timeZone"
    @close="closeCalendarSchedulingResourceActionModal"
  />

  <CalendarConflictDetailModal
    v-if="selectedConflictEventId && selectedConflict"
    :conflict="selectedConflict"
    :current-event-id="selectedConflictEventId"
    :time-zone="props.model.timeZone"
    :error-message="conflictErrorMessage"
    :loading="conflictOverrideLoading"
    @close="closeConflictDetail"
    @edit-event="handleConflictEventEdit"
    @override="handleConflictOverride"
  />
</template>

<style scoped>
.calendar-scheduling-event-block {
  position: relative;
}

.calendar-scheduling-event-block.has-conflict-overlay {
  z-index: 5;
}

.calendar-scheduling-header {
  position: relative;
}

.calendar-scheduling-assignment-definition {
  cursor: pointer;
  min-width: 0;
  width: 100%;
}

.calendar-scheduling-assignment-definition:focus-visible {
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: 2px;
}

.calendar-scheduling-assignment-definition__content {
  display: grid;
  gap: 0.25rem;
}

.calendar-scheduling-assignment-definition__name {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  overflow-wrap: anywhere;
}

.calendar-scheduling-assignment-definition__time,
.calendar-scheduling-assignment-definition__subtitle {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
}

.calendar-scheduling-assignment-definition__slots {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
  margin-top: 0.125rem;
}

.calendar-scheduling-assignment-definition__slot {
  background: rgb(var(--ua-calendar-panel-bg));
  border: 1px solid var(--ua-border-color);
  border-radius: 0.1875rem;
  display: inline-block;
  height: 0.75rem;
  width: 0.75rem;
}

.calendar-scheduling-team-member {
  align-items: center;
  display: flex;
  gap: var(--ua-spacing-sm);
  min-width: 0;
  width: 100%;
}

.calendar-scheduling-team-member__avatar {
  align-items: center;
  background: rgb(var(--v-theme-surface-variant));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  display: inline-flex;
  flex: 0 0 2rem;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 2rem;
  justify-content: center;
  width: 2rem;
}

.calendar-scheduling-team-member__content {
  display: grid;
  gap: 0.125rem;
  min-width: 0;
}

.calendar-scheduling-team-member__name {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  overflow-wrap: anywhere;
}

.calendar-scheduling-team-member__position,
.calendar-scheduling-team-member__meta {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  overflow-wrap: anywhere;
}

.calendar-scheduling-team-member__meta-list {
  display: grid;
  gap: 0.0625rem;
}

.calendar-scheduling-team-member__meta-label {
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-scheduling-active-shift-choice {
  display: grid;
  gap: var(--ua-spacing-md);
}

.calendar-scheduling-active-shift-choice__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-scheduling-active-shift-choice__details {
  background: rgb(var(--ua-calendar-panel-bg));
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  color: var(--ua-text-primary);
  font-family: inherit;
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
  padding: var(--ua-spacing-md);
  white-space: pre-wrap;
}
</style>
