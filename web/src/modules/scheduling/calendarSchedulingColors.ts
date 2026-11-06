import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';

export const calendarMatrixColorMap = {
  white: '#9A9A9A',
  black: '#000000',
  red: '#B65C64',
  green: '#6E9B74',
  yellow: '#C7A94E',
  blue: '#5F79B8',
  orange: '#BF7A4A',
  purple: '#85669F',
  cyan: '#6FA9B5',
  magenta: '#B36A9E',
  lime: '#9CA85B',
  pink: '#C58A9C',
  teal: '#5F938D',
  lavender: '#A899C9',
  brown: '#8E6D4F',
  navy: '#3F4E7A',
} as const;

export type CalendarMatrixColorId = keyof typeof calendarMatrixColorMap;

export const calendarNonSchedulingEventColors = {
  holiday: 'var(--calendar-event-holiday-color)',
  training: 'rgb(var(--v-theme-info))',
  leave: calendarMatrixColorMap.purple,
} as const;

export function resolveSchedulingEventBorderStyle(event: {
  type?: string | null;
  eventTypeCode?: string | null;
  statusTypeCode?: string | null;
}): 'solid' | 'dotted' {
  const eventType = event.type?.trim().toLowerCase();
  const eventTypeCode = event.eventTypeCode?.trim().toLowerCase();
  const isShiftOrAssignment =
    eventType === 'scheduling.shift' ||
    eventType === 'scheduling.assignment' ||
    eventTypeCode === 'shift' ||
    eventTypeCode === 'assignment';

  return isShiftOrAssignment && normalizeSchedulingLifecycleStatus(event.statusTypeCode) === 'draft'
    ? 'dotted'
    : 'solid';
}
