import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export const calendarSchedulingResourceActionResource = ref<CalendarMatrixResource>();
export const isCalendarSchedulingResourceActionModalOpen = ref(false);
export const calendarSchedulingResourceActionDate = ref<string>();
export const calendarSchedulingConflictEventId = ref<string>();
export const calendarSchedulingDetailEvent = ref<CalendarEventBase>();

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
