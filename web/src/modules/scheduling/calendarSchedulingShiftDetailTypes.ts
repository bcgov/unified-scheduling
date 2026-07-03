export type ShiftDetailTabId = 'details' | 'edit' | 'transfer' | 'copy' | 'delete';
export type ShiftOpenScope = 'event' | 'series';

export type ShiftDetailRow = {
  label: string;
  value: string;
  recurrenceRule?: string | null;
  recurrenceStartDate?: string | null;
};
