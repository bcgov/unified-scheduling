import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixCellDropContext,
  CalendarMatrixDragPayload,
  CalendarMatrixResource,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export interface CalendarMatrixTestDropModalState {
  drag: CalendarMatrixDragPayload;
  drop: CalendarMatrixCellDropContext;
}

export const isCalendarMatrixTestAssignmentModalOpen = ref(false);
export const calendarMatrixTestResourceActionResource = ref<CalendarMatrixResource>();
export const calendarMatrixTestDropModalState = ref<CalendarMatrixTestDropModalState>();
export const calendarMatrixTestEventActionEvent = ref<CalendarEventBase>();
export const calendarMatrixTestConflictEventId = ref<string>();
export const calendarMatrixTestDetailEvent = ref<CalendarEventBase>();

export function showCalendarMatrixTestAssignmentModal() {
  isCalendarMatrixTestAssignmentModalOpen.value = true;
}

export function closeCalendarMatrixTestAssignmentModal() {
  isCalendarMatrixTestAssignmentModalOpen.value = false;
}

export function showCalendarMatrixTestResourceActionModal(resource: CalendarMatrixResource) {
  calendarMatrixTestResourceActionResource.value = resource;
}

export function closeCalendarMatrixTestResourceActionModal() {
  calendarMatrixTestResourceActionResource.value = undefined;
}

export function showCalendarMatrixTestDropModal(drag: CalendarMatrixDragPayload, drop: CalendarMatrixCellDropContext) {
  calendarMatrixTestDropModalState.value = {
    drag,
    drop,
  };
}

export function closeCalendarMatrixTestDropModal() {
  calendarMatrixTestDropModalState.value = undefined;
}

export function showCalendarMatrixTestEventActionModal(event: CalendarEventBase) {
  calendarMatrixTestEventActionEvent.value = event;
}

export function closeCalendarMatrixTestEventActionModal() {
  calendarMatrixTestEventActionEvent.value = undefined;
}

export function toggleCalendarMatrixTestConflict(eventId: string) {
  calendarMatrixTestConflictEventId.value = calendarMatrixTestConflictEventId.value === eventId ? undefined : eventId;
}

export function closeCalendarMatrixTestConflict() {
  calendarMatrixTestConflictEventId.value = undefined;
}

export function showCalendarMatrixTestEventDetail(event: CalendarEventBase) {
  calendarMatrixTestDetailEvent.value = event;
}

export function closeCalendarMatrixTestEventDetail() {
  calendarMatrixTestDetailEvent.value = undefined;
}
