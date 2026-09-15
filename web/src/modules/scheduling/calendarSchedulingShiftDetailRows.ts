import type { SelectOption } from '@/types/select';

export function formatAssigneeIds(userIds: string[], employeeOptions: SelectOption[]) {
  if (userIds.length === 0) {
    return 'None';
  }

  return userIds
    .map((userId) => employeeOptions.find((option) => String(option.code) === userId)?.description || userId)
    .join(', ');
}
