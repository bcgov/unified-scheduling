import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixCell,
  CalendarMatrixResource,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export const isCalendarSchedulingAssignmentModalOpen = ref(false);
export const calendarSchedulingAssignmentTypeCreateRequest = ref<{ locationId?: number; startDate: string }>();
export const calendarSchedulingAssignmentModalMode = ref<'create' | 'view' | 'edit'>('create');
export const calendarSchedulingAssignmentModalEditScope = ref<'event' | 'series'>();
export const calendarSchedulingAssignmentModalDate = ref<string>();
export const calendarSchedulingAssignmentModalEntryId = ref<number>();
export const calendarSchedulingAssignmentModalSeriesId = ref<number>();
export const calendarSchedulingAssignmentModalAssignmentDefinitionId = ref<number>();
export const calendarSchedulingAssignmentModalShiftEntryIds = ref<number[]>();
export const calendarSchedulingAssignmentModalAssignedUserId = ref<string>();
export const calendarSchedulingAssignmentModalDraggedUserOnly = ref(false);
export const calendarSchedulingResourceActionResource = ref<CalendarMatrixResource>();
export const isCalendarSchedulingResourceActionModalOpen = ref(false);
export const calendarSchedulingResourceActionDate = ref<string>();
export const calendarSchedulingResourceActionAssignmentEntryId = ref<number>();
export const calendarSchedulingResourceActionAssignmentEvents = ref<CalendarEventBase[]>([]);
export const calendarSchedulingResourceActionMissingShiftWarning = ref(false);
export const calendarSchedulingConflictEventId = ref<string>();
export const calendarSchedulingConflictHeaderId = ref<string>();
export const calendarSchedulingDetailEvent = ref<CalendarEventBase>();
export const calendarSchedulingDetailInitialOpenScope = ref<'event' | 'series'>();
export const calendarSchedulingDetailInitialTab = ref<'details' | 'edit'>();
export const calendarSchedulingLeaveDetailEvent = ref<CalendarEventBase>();
export const calendarSchedulingPublishRequest = ref<{
  locationId: number;
  startDate: string;
  endDate: string;
}>();

export function showCalendarSchedulingAssignmentTypeCreateModal(locationId: number | undefined, startDate: string) {
  calendarSchedulingAssignmentTypeCreateRequest.value = { locationId, startDate };
}

export function closeCalendarSchedulingAssignmentTypeCreateModal() {
  calendarSchedulingAssignmentTypeCreateRequest.value = undefined;
}

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
    draggedUserOnly?: boolean;
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
  calendarSchedulingAssignmentModalDraggedUserOnly.value = options?.draggedUserOnly ?? false;
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
  calendarSchedulingAssignmentModalDraggedUserOnly.value = false;
}

export function showCalendarSchedulingResourceActionModal(
  resource?: CalendarMatrixResource,
  date?: string,
  options?: {
    assignmentEntryId?: number;
    assignmentEvents?: CalendarEventBase[];
    missingShiftWarning?: boolean;
  },
) {
  isCalendarSchedulingResourceActionModalOpen.value = true;
  calendarSchedulingResourceActionResource.value = resource;
  calendarSchedulingResourceActionDate.value = date;
  calendarSchedulingResourceActionAssignmentEntryId.value = options?.assignmentEntryId;
  calendarSchedulingResourceActionAssignmentEvents.value = options?.assignmentEvents ?? [];
  calendarSchedulingResourceActionMissingShiftWarning.value = options?.missingShiftWarning ?? false;
}

export function closeCalendarSchedulingResourceActionModal() {
  isCalendarSchedulingResourceActionModalOpen.value = false;
  calendarSchedulingResourceActionResource.value = undefined;
  calendarSchedulingResourceActionDate.value = undefined;
  calendarSchedulingResourceActionAssignmentEntryId.value = undefined;
  calendarSchedulingResourceActionAssignmentEvents.value = [];
  calendarSchedulingResourceActionMissingShiftWarning.value = false;
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
  options?: { initialOpenScope?: 'event' | 'series'; initialTab?: 'details' | 'edit' },
) {
  calendarSchedulingDetailEvent.value = event;
  calendarSchedulingDetailInitialOpenScope.value = options?.initialOpenScope;
  calendarSchedulingDetailInitialTab.value = options?.initialTab;
}

export function closeCalendarSchedulingEventDetail() {
  calendarSchedulingDetailEvent.value = undefined;
  calendarSchedulingDetailInitialOpenScope.value = undefined;
  calendarSchedulingDetailInitialTab.value = undefined;
}

export function showCalendarSchedulingLeaveDetail(event: CalendarEventBase) {
  calendarSchedulingLeaveDetailEvent.value = event;
}

export function closeCalendarSchedulingLeaveDetail() {
  calendarSchedulingLeaveDetailEvent.value = undefined;
}

export function showCalendarSchedulingPublishModal(locationId: number, startDate: string, endDate: string) {
  calendarSchedulingPublishRequest.value = { locationId, startDate, endDate };
}

export function closeCalendarSchedulingPublishModal() {
  calendarSchedulingPublishRequest.value = undefined;
}
