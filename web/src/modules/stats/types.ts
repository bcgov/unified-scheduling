export interface AssignmentData {
  id: string;
  groupId: number | null;
  categoryId: number | null;
  subCategoryId: number | null;
  /** subCategoryMetricId → string input value */
  metricValues: Record<number, string>;
  comment: string;
}

export interface DayAssignment extends AssignmentData {
  /** subCategoryMetricId → existing server record ID (for PUT/DELETE) */
  existingRecordIds: Record<number, number>;
}

export interface DaySummary {
  regularHours: number;
  overtimeHours: number;
  assignmentCount: number;
}

export type EntryStatus = 'Draft' | 'Submitted' | 'SignedOff' | '';
