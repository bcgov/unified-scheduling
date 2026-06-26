import type { SelectOption } from '@/types/select';

export const USER_ROLE_EXPIRY_REASON_OPTIONS: SelectOption[] = [
  { code: 'OPERDEMAND', description: 'Cover Operational Demands' },
  { code: 'PERSONAL', description: 'Personal Decision' },
  { code: 'ENTRYERR', description: 'Entry Error' },
];
