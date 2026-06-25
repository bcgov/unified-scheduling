export const DAILY_REGULAR_TARGET_HOURS = 7;
export const WEEKLY_REGULAR_TARGET_HOURS = 35;

export const EntryStatus = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  SignedOff: 'SignedOff',
} as const;

// Maps stat group DB IDs to their entry form route names.
// Group 1 = Non-Supervision, Group 2 = Supervision (see StatsModule.ts route definitions).
export const GROUP_ROUTE: Record<number, string> = {
  1: 'NonSupervisionForm',
  2: 'SupervisionForm',
};
