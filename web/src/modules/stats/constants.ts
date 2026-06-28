export const DAILY_REGULAR_TARGET_HOURS = 7;
export const WEEKLY_REGULAR_TARGET_HOURS = 35;

export const EntryStatus = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  SignedOff: 'SignedOff',
} as const;

export const STATUS_COLORS: Partial<Record<string, string>> = {
  [EntryStatus.SignedOff]: 'primary',
  [EntryStatus.Submitted]: 'success',
  [EntryStatus.Draft]: 'warning',
};

export function statusColor(status?: string): string {
  return status ? (STATUS_COLORS[status] ?? 'default') : 'default';
}

// Maps stat group DB IDs to their entry form route names.
// Group 1 = Non-Supervision, Group 2 = Supervision (see StatsModule.ts route definitions).
export const GROUP_ROUTE: Record<number, string> = {
  1: 'NonSupervisionForm',
  2: 'SupervisionForm',
};
