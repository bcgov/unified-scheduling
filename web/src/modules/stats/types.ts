export interface AssignmentData {
  id: string;
  groupId: number | null;
  categoryId: number | null;
  subCategoryId: number | null;
  /** subCategoryMetricId → string input value */
  metricValues: Record<number, string>;
  comment: string;
}

export type PeriodType = 'Daily' | 'Weekly' | 'Monthly';
