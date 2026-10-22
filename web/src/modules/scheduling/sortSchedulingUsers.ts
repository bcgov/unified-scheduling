import type { LookupCodeResponse, UserResponse } from '@/api-access/generated/models';

const nameCollator = new Intl.Collator(undefined, { sensitivity: 'base' });

export function sortSchedulingUsers(users: readonly UserResponse[], positions: Record<string, LookupCodeResponse>) {
  const orderFor = (user: UserResponse) => {
    const order = user.rank ? positions[user.rank]?.displayOrder : undefined;
    return typeof order === 'number' && Number.isFinite(order) ? order : Number.POSITIVE_INFINITY;
  };

  return [...users].sort((left, right) => {
    const leftOrder = orderFor(left);
    const rightOrder = orderFor(right);
    if (leftOrder !== rightOrder) return leftOrder - rightOrder;
    return (
      nameCollator.compare(left.lastName ?? '', right.lastName ?? '') ||
      nameCollator.compare(left.firstName ?? '', right.firstName ?? '') ||
      left.id.localeCompare(right.id)
    );
  });
}
