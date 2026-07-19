import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export const isCalendarSchedulingAssignmentModalOpen = ref(false);
export const calendarSchedulingAssignmentModalMode = ref<'create' | 'view' | 'edit'>('create');
export const calendarSchedulingAssignmentModalEditScope = ref<'event' | 'series'>('event');
export const calendarSchedulingAssignmentModalDate = ref<string>();
export const calendarSchedulingAssignmentModalEntryId = ref<number>();
export const calendarSchedulingAssignmentModalSeriesId = ref<number>();
export const calendarSchedulingAssignmentModalAssignmentDefinitionId = ref<number>();
export const calendarSchedulingAssignmentModalShiftEntryIds = ref<number[]>();
export const calendarSchedulingResourceActionResource = ref<CalendarMatrixResource>();
export const isCalendarSchedulingResourceActionModalOpen = ref(false);
export const calendarSchedulingResourceActionDate = ref<string>();
export const calendarSchedulingResourceActionAssignmentEntryId = ref<number>();
export const calendarSchedulingResourceActionAssignmentEvents = ref<CalendarEventBase[]>([]);
export const calendarSchedulingEventActionEvent = ref<CalendarEventBase>();
export const calendarSchedulingConflictEventId = ref<string>();
export const calendarSchedulingDetailEvent = ref<CalendarEventBase>();

export function showCalendarSchedulingAssignmentModal(
  date?: string,
  options?: {
    mode?: 'create' | 'view' | 'edit';
    editScope?: 'event' | 'series';
    assignmentEntryId?: number;
    assignmentSeriesId?: number;
    assignmentDefinitionId?: number;
    shiftEntryIds?: number[];
  },
) {
  calendarSchedulingAssignmentModalMode.value = options?.mode ?? 'create';
  calendarSchedulingAssignmentModalEditScope.value = options?.editScope ?? 'event';
  calendarSchedulingAssignmentModalDate.value = date;
  calendarSchedulingAssignmentModalEntryId.value = options?.assignmentEntryId;
  calendarSchedulingAssignmentModalSeriesId.value = options?.assignmentSeriesId;
  calendarSchedulingAssignmentModalAssignmentDefinitionId.value = options?.assignmentDefinitionId;
  calendarSchedulingAssignmentModalShiftEntryIds.value = options?.shiftEntryIds;
  isCalendarSchedulingAssignmentModalOpen.value = true;
}

export function closeCalendarSchedulingAssignmentModal() {
  isCalendarSchedulingAssignmentModalOpen.value = false;
  calendarSchedulingAssignmentModalMode.value = 'create';
  calendarSchedulingAssignmentModalEditScope.value = 'event';
  calendarSchedulingAssignmentModalDate.value = undefined;
  calendarSchedulingAssignmentModalEntryId.value = undefined;
  calendarSchedulingAssignmentModalSeriesId.value = undefined;
  calendarSchedulingAssignmentModalAssignmentDefinitionId.value = undefined;
  calendarSchedulingAssignmentModalShiftEntryIds.value = undefined;
}

export function showCalendarSchedulingResourceActionModal(
  resource?: CalendarMatrixResource,
  date?: string,
  options?: {
    assignmentEntryId?: number;
    assignmentEvents?: CalendarEventBase[];
  },
) {
  calendarSchedulingResourceActionResource.value = resource;
  calendarSchedulingResourceActionDate.value = date;
  calendarSchedulingResourceActionAssignmentEntryId.value = options?.assignmentEntryId;
  calendarSchedulingResourceActionAssignmentEvents.value = options?.assignmentEvents ?? [];
  isCalendarSchedulingResourceActionModalOpen.value = true;
}

export function closeCalendarSchedulingResourceActionModal() {
  isCalendarSchedulingResourceActionModalOpen.value = false;
  calendarSchedulingResourceActionResource.value = undefined;
  calendarSchedulingResourceActionDate.value = undefined;
  calendarSchedulingResourceActionAssignmentEntryId.value = undefined;
  calendarSchedulingResourceActionAssignmentEvents.value = [];
}

export function showCalendarSchedulingEventActionModal(event: CalendarEventBase) {
  calendarSchedulingEventActionEvent.value = event;
}

export function closeCalendarSchedulingEventActionModal() {
  calendarSchedulingEventActionEvent.value = undefined;
}

export function toggleCalendarSchedulingConflict(eventId: string) {
  calendarSchedulingConflictEventId.value = calendarSchedulingConflictEventId.value === eventId ? undefined : eventId;
}

export function closeCalendarSchedulingConflict() {
  calendarSchedulingConflictEventId.value = undefined;
}

export function showCalendarSchedulingEventDetail(event: CalendarEventBase) {
  calendarSchedulingDetailEvent.value = event;
}

export function closeCalendarSchedulingEventDetail() {
  calendarSchedulingDetailEvent.value = undefined;
}
