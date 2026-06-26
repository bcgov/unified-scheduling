import type { CalendarEventBase } from '../../calendarTypes';

export const CalendarMatrixEventGroupVariant = {
  Primary: 'primary',
  Secondary: 'secondary',
  Muted: 'muted',
  Warning: 'warning',
} as const;

export type CalendarMatrixEventGroupVariant =
  (typeof CalendarMatrixEventGroupVariant)[keyof typeof CalendarMatrixEventGroupVariant];

export interface CalendarMatrixMetaItem {
  label?: string;
  value: string;
}

export const CalendarMatrixActionType = {
  Button: 'button',
  Custom: 'custom',
  Menu: 'menu',
} as const;

export type CalendarMatrixActionType = (typeof CalendarMatrixActionType)[keyof typeof CalendarMatrixActionType];

export interface CalendarMatrixActionDisplay {
  actionId: string;
  text?: string;
  icon?: string;
  ariaLabel?: string;
  type: CalendarMatrixActionType;
}

export interface CalendarMatrixEventBlockActionEvent {
  event: CalendarEventBase;
  actionId: string;
  actionType: CalendarMatrixActionType;
}

export interface CalendarMatrixResourceActionDisplay {
  actionId: string;
  label?: string;
  icon?: string;
  ariaLabel: string;
}

export interface CalendarMatrixEventDisplay {
  color?: string;
  status?: string;
  draggable?: boolean;
  action?: CalendarMatrixActionDisplay;
}

export interface CalendarMatrixDay {
  date: string;
  label: string;
  isToday?: boolean;
}

export interface CalendarMatrixResource {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  meta?: CalendarMatrixMetaItem[];
  avatarText?: string;
  action?: CalendarMatrixResourceActionDisplay;
}

export interface CalendarMatrixEventItem {
  event: CalendarEventBase;
  display?: CalendarMatrixEventDisplay;
}

export interface CalendarMatrixEventGroup {
  id: string;
  label?: string;
  events: CalendarMatrixEventItem[];
  variant?: CalendarMatrixEventGroupVariant;
  color?: string;
  showColorBar?: boolean;
  action?: CalendarMatrixActionDisplay;
}

export interface CalendarMatrixCell {
  resourceId: string;
  date: string;
  headers?: CalendarMatrixCellHeader[];
  groups: CalendarMatrixEventGroup[];
}

export interface CalendarMatrixCellHeader {
  id?: string;
  text: string;
  title?: string;
  status?: string;
  color?: string;
  actionId?: string;
  action?: CalendarMatrixActionDisplay;
  payload?: unknown;
}

export interface CalendarMatrixCellHeaderClickEvent {
  cell: CalendarMatrixCell;
  header: CalendarMatrixCellHeader;
}

export interface CalendarMatrixCellHeaderActionEvent {
  cell: CalendarMatrixCell;
  header: CalendarMatrixCellHeader;
  actionId: string;
  actionType: CalendarMatrixActionType;
}

export interface CalendarMatrixSidePanel {
  label: string;
  items: CalendarMatrixSidePanelItem[];
  actionId?: string;
  actionLabel?: string;
}

export interface CalendarMatrixSidePanelItem {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  meta?: CalendarMatrixMetaItem[];
  avatarText?: string;
  draggable?: boolean;
  payload?: unknown;
}

export interface CalendarMatrixViewModel {
  unsupportedMessage?: string;
  timeZone?: string;
  days: CalendarMatrixDay[];
  primaryColumn: {
    label: string;
    resources: CalendarMatrixResource[];
  };
  cells: CalendarMatrixCell[];
  sidePanel?: CalendarMatrixSidePanel;
}

export interface CalendarMatrixDragPayload {
  source: 'side-panel' | 'event-block';
  itemId: string;
  itemType: string;
  payload?: unknown;
}

export interface CalendarMatrixCellDropContext {
  resourceId: string;
  resourceType?: string;
  date: string;
}

export interface CalendarMatrixCellDropEvent {
  drag: CalendarMatrixDragPayload;
  drop: CalendarMatrixCellDropContext;
}
