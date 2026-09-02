import type { SelectOption } from '@/types/select';

export const AUDIT_ACTIONS = ['Added', 'Modified', 'Deleted'] as const;
export type AuditAction = (typeof AUDIT_ACTIONS)[number];

export const AUDIT_ACTION_COLORS: Record<AuditAction, string> = {
  Added: 'success',
  Modified: 'info',
  Deleted: 'error',
};

export const AUDIT_ACTION_OPTIONS: SelectOption[] = [
  { code: '', description: 'All' },
  ...AUDIT_ACTIONS.map((action) => ({ code: action, description: action })),
];

export const DEFAULT_PAGE_SIZE = 10;
