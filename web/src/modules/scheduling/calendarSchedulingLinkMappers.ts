export interface AssignedUserLink {
  assignedUserIds?: unknown;
  userIds?: unknown;
}

export interface NumericAssignedUserLink extends AssignedUserLink {
  [key: string]: unknown;
}

export function filterAssignedUserIds(link: AssignedUserLink) {
  const userIds = Array.isArray(link.assignedUserIds)
    ? link.assignedUserIds
    : Array.isArray(link.userIds)
      ? link.userIds
      : [];

  return filterStringArray(userIds);
}

export function filterStringArray(value: unknown) {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

export function parsePositiveInteger(value: unknown) {
  const parsed = typeof value === 'number' ? value : typeof value === 'string' ? Number(value) : NaN;
  return Number.isInteger(parsed) && parsed > 0 ? [parsed] : [];
}

export function dedupeLinksById<TLink, TKey extends keyof TLink>(links: TLink[], idKey: TKey) {
  const byId = new Map<TLink[TKey], TLink>();
  for (const link of links) {
    byId.set(link[idKey], link);
  }
  return Array.from(byId.values());
}

export function mapSelectedIdsToAssignedUserLinks<TKey extends string>(
  ids: unknown[] | undefined,
  idKey: TKey,
  assignedUserIds: string[],
): Array<Record<TKey, number> & { assignedUserIds: string[] }> {
  return [...new Set(ids?.flatMap(parsePositiveInteger) ?? [])].map((id) => ({
    [idKey]: id,
    assignedUserIds,
  })) as Array<Record<TKey, number> & { assignedUserIds: string[] }>;
}
