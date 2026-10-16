import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixCell,
  CalendarMatrixResource,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export const isCalendarSchedulingAssignmentModalOpen = ref(false);
export const calendarSchedulingAssignmentModalMode = ref<'create' | 'view' | 'edit'>('create');
export const calendarSchedulingAssignmentModalEditScope = ref<'event' | 'series'>();
export const calendarSchedulingAssignmentModalDate = ref<string>();
export const calendarSchedulingAssignmentModalEntryId = ref<number>();
export const calendarSchedulingAssignmentModalSeriesId = ref<number>();
export const calendarSchedulingAssignmentModalAssignmentDefinitionId = ref<number>();
export const calendarSchedulingAssignmentModalShiftEntryIds = ref<number[]>();
export const calendarSchedulingAssignmentModalAssignedUserId = ref<string>();
export const calendarSchedulingResourceActionResource = ref<CalendarMatrixResource>();
export const isCalendarSchedulingResourceActionModalOpen = ref(false);
export const calendarSchedulingResourceActionDate = ref<string>();
export const calendarSchedulingResourceActionAssignmentEntryId = ref<number>();
export const calendarSchedulingResourceActionAssignmentEvents = ref<CalendarEventBase[]>([]);
export const calendarSchedulingConflictEventId = ref<string>();
export const calendarSchedulingConflictHeaderId = ref<string>();
export const calendarSchedulingDetailEvent = ref<CalendarEventBase>();
export const calendarSchedulingDetailInitialOpenScope = ref<'event' | 'series'>();
export const calendarSchedulingTimeOffDetailEvent = ref<CalendarEventBase>();
export const calendarSchedulingExistingShiftChoice = ref<{
  shiftEvent: CalendarEventBase;
  resource: CalendarMatrixResource;
  date: string;
  assignmentEntryId?: number;
  assignmentEvents: CalendarEventBase[];
}>();

export function showCalendarSchedulingAssignmentModal(
  date?: string,
  options?: {
    mode?: 'create' | 'view' | 'edit';
    editScope?: 'event' | 'series';
    assignmentEntryId?: number;
    assignmentSeriesId?: number;
    assignmentDefinitionId?: number;
    shiftEntryIds?: number[];
    assignedUserId?: string;
  },
) {
  calendarSchedulingDetailEvent.value = undefined;
  calendarSchedulingDetailInitialOpenScope.value = undefined;
  calendarSchedulingAssignmentModalMode.value = options?.mode ?? 'create';
  calendarSchedulingAssignmentModalEditScope.value = options?.editScope;
  isCalendarSchedulingAssignmentModalOpen.value = true;
  calendarSchedulingAssignmentModalDate.value = date;
  calendarSchedulingAssignmentModalEntryId.value = options?.assignmentEntryId;
  calendarSchedulingAssignmentModalSeriesId.value = options?.assignmentSeriesId;
  calendarSchedulingAssignmentModalAssignmentDefinitionId.value = options?.assignmentDefinitionId;
  calendarSchedulingAssignmentModalShiftEntryIds.value = options?.shiftEntryIds;
  calendarSchedulingAssignmentModalAssignedUserId.value = options?.assignedUserId;
}

export function closeCalendarSchedulingAssignmentModal() {
  isCalendarSchedulingAssignmentModalOpen.value = false;
  calendarSchedulingAssignmentModalMode.value = 'create';
  calendarSchedulingAssignmentModalEditScope.value = undefined;
  calendarSchedulingAssignmentModalDate.value = undefined;
  calendarSchedulingAssignmentModalEntryId.value = undefined;
  calendarSchedulingAssignmentModalSeriesId.value = undefined;
  calendarSchedulingAssignmentModalAssignmentDefinitionId.value = undefined;
  calendarSchedulingAssignmentModalShiftEntryIds.value = undefined;
  calendarSchedulingAssignmentModalAssignedUserId.value = undefined;
}

export function showCalendarSchedulingResourceActionModal(
  resource?: CalendarMatrixResource,
  date?: string,
  options?: {
    assignmentEntryId?: number;
    assignmentEvents?: CalendarEventBase[];
  },
) {
  isCalendarSchedulingResourceActionModalOpen.value = true;
  calendarSchedulingResourceActionResource.value = resource;
  calendarSchedulingResourceActionDate.value = date;
  calendarSchedulingResourceActionAssignmentEntryId.value = options?.assignmentEntryId;
  calendarSchedulingResourceActionAssignmentEvents.value = options?.assignmentEvents ?? [];
}

export function closeCalendarSchedulingResourceActionModal() {
  isCalendarSchedulingResourceActionModalOpen.value = false;
  calendarSchedulingResourceActionResource.value = undefined;
  calendarSchedulingResourceActionDate.value = undefined;
  calendarSchedulingResourceActionAssignmentEntryId.value = undefined;
  calendarSchedulingResourceActionAssignmentEvents.value = [];
}

export function showCalendarSchedulingExistingShiftChoice(options: {
  shiftEvent: CalendarEventBase;
  resource: CalendarMatrixResource;
  date: string;
  assignmentEntryId?: number;
  assignmentEvents: CalendarEventBase[];
}) {
  calendarSchedulingExistingShiftChoice.value = options;
}

export function closeCalendarSchedulingExistingShiftChoice() {
  calendarSchedulingExistingShiftChoice.value = undefined;
}

export function toggleCalendarSchedulingConflict(eventId: string) {
  calendarSchedulingConflictEventId.value = calendarSchedulingConflictEventId.value === eventId ? undefined : eventId;
  calendarSchedulingConflictHeaderId.value = undefined;
}

export function getCalendarSchedulingHeaderConflictKey(
  cell: Pick<CalendarMatrixCell, 'resourceId' | 'date'>,
  headerId?: string,
) {
  if (!headerId) {
    return undefined;
  }

  return `${cell.resourceId}:${cell.date}:${headerId}`;
}

export function toggleCalendarSchedulingHeaderConflict(
  cell: Pick<CalendarMatrixCell, 'resourceId' | 'date'>,
  headerId: string,
) {
  const conflictKey = getCalendarSchedulingHeaderConflictKey(cell, headerId);
  calendarSchedulingConflictHeaderId.value =
    calendarSchedulingConflictHeaderId.value === conflictKey ? undefined : conflictKey;
  calendarSchedulingConflictEventId.value = undefined;
}

export function closeCalendarSchedulingConflict() {
  calendarSchedulingConflictEventId.value = undefined;
  calendarSchedulingConflictHeaderId.value = undefined;
}

export function showCalendarSchedulingEventDetail(
  event: CalendarEventBase,
  options?: { initialOpenScope?: 'event' | 'series' },
) {
  calendarSchedulingDetailEvent.value = event;
  calendarSchedulingDetailInitialOpenScope.value = options?.initialOpenScope;
}

export function closeCalendarSchedulingEventDetail() {
  calendarSchedulingDetailEvent.value = undefined;
  calendarSchedulingDetailInitialOpenScope.value = undefined;
}

export function showCalendarSchedulingTimeOffDetail(event: CalendarEventBase) {
  calendarSchedulingTimeOffDetailEvent.value = event;
}

export function closeCalendarSchedulingTimeOffDetail() {
  calendarSchedulingTimeOffDetailEvent.value = undefined;
}
