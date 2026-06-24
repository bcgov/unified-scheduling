import type { InjectionKey, Ref } from 'vue';
import type { CalendarMatrixCellDropContext, CalendarMatrixDragPayload } from './calendarMatrixTypes';

export interface CalendarMatrixContext {
  selectedEventId: Ref<string | undefined>;
  selectedResourceId: Ref<string | undefined>;
  activeDragPayload: Ref<CalendarMatrixDragPayload | undefined>;
  selectEvent: (eventId: string) => void;
  selectResource: (resourceId: string) => void;
  startDrag: (payload: CalendarMatrixDragPayload) => void;
  clearDrag: () => void;
  dropOnCell: (context: CalendarMatrixCellDropContext) => void;
}

export const calendarMatrixContextKey: InjectionKey<CalendarMatrixContext> = Symbol('calendarMatrixContext');
