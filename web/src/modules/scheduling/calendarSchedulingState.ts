import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type {
  CalendarMatrixCellDropContext,
  CalendarMatrixDragPayload,
  CalendarMatrixResource,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export interface CalendarSchedulingDropModalState {
  drag: CalendarMatrixDragPayload;
  drop: CalendarMatrixCellDropContext;
}

export const isCalendarSchedulingAssignmentModalOpen = ref(false);
export const calendarSchedulingResourceActionResource = ref<CalendarMatrixResource>();
export const isCalendarSchedulingResourceActionModalOpen = ref(false);
export const calendarSchedulingResourceActionDate = ref<string>();
export const calendarSchedulingDropModalState = ref<CalendarSchedulingDropModalState>();
export const calendarSchedulingEventActionEvent = ref<CalendarEventBase>();
export const calendarSchedulingConflictEventId = ref<string>();
export const calendarSchedulingDetailEvent = ref<CalendarEventBase>();

export function showCalendarSchedulingAssignmentModal() {
  isCalendarSchedulingAssignmentModalOpen.value = true;
}

export function closeCalendarSchedulingAssignmentModal() {
  isCalendarSchedulingAssignmentModalOpen.value = false;
}

export function showCalendarSchedulingResourceActionModal(resource?: CalendarMatrixResource, date?: string) {
  isCalendarSchedulingResourceActionModalOpen.value = true;
  calendarSchedulingResourceActionResource.value = resource;
  calendarSchedulingResourceActionDate.value = date;
}

export function closeCalendarSchedulingResourceActionModal() {
  isCalendarSchedulingResourceActionModalOpen.value = false;
  calendarSchedulingResourceActionResource.value = undefined;
  calendarSchedulingResourceActionDate.value = undefined;
}

export function showCalendarSchedulingDropModal(drag: CalendarMatrixDragPayload, drop: CalendarMatrixCellDropContext) {
  calendarSchedulingDropModalState.value = {
    drag,
    drop,
  };
}

export function closeCalendarSchedulingDropModal() {
  calendarSchedulingDropModalState.value = undefined;
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
