import { ref } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

export const isCalendarSchedulingAssignmentModalOpen = ref(false);
export const calendarSchedulingAssignmentModalMode = ref<'create' | 'view' | 'edit'>('create');
export const calendarSchedulingAssignmentModalInitialTab = ref<'details' | 'edit' | 'delete'>('details');
export const calendarSchedulingAssignmentModalEditScope = ref<'event' | 'series'>();
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
export const calendarSchedulingDetailInitialOpenScope = ref<'event' | 'series'>();
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
    initialTab?: 'details' | 'edit' | 'delete';
    editScope?: 'event' | 'series';
    assignmentEntryId?: number;
    assignmentSeriesId?: number;
    assignmentDefinitionId?: number;
    shiftEntryIds?: number[];
  },
) {
  calendarSchedulingDetailEvent.value = undefined;
  calendarSchedulingDetailInitialOpenScope.value = undefined;
  calendarSchedulingAssignmentModalMode.value = options?.mode ?? 'create';
  calendarSchedulingAssignmentModalInitialTab.value = options?.initialTab ?? 'details';
  calendarSchedulingAssignmentModalEditScope.value = options?.editScope;
  isCalendarSchedulingAssignmentModalOpen.value = true;
  calendarSchedulingAssignmentModalDate.value = date;
  calendarSchedulingAssignmentModalEntryId.value = options?.assignmentEntryId;
  calendarSchedulingAssignmentModalSeriesId.value = options?.assignmentSeriesId;
  calendarSchedulingAssignmentModalAssignmentDefinitionId.value = options?.assignmentDefinitionId;
  calendarSchedulingAssignmentModalShiftEntryIds.value = options?.shiftEntryIds;
}

export function closeCalendarSchedulingAssignmentModal() {
  isCalendarSchedulingAssignmentModalOpen.value = false;
  calendarSchedulingAssignmentModalMode.value = 'create';
  calendarSchedulingAssignmentModalInitialTab.value = 'details';
  calendarSchedulingAssignmentModalEditScope.value = undefined;
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
