import { parsePositiveInteger } from './calendarSchedulingShiftIds';

export interface AssignedUserLink {
  assignedUserIds?: unknown;
  userIds?: unknown;
}

export interface NumericAssignedUserLink extends AssignedUserLink {
  [key: string]: unknown;
}

export function filterAssignedUserIds(link: AssignedUserLink) {
  let userIds: unknown[] = [];
  if (Array.isArray(link.assignedUserIds)) {
    userIds = link.assignedUserIds;
  } else if (Array.isArray(link.userIds)) {
    userIds = link.userIds;
  }

  return filterStringArray(userIds);
}

export function filterStringArray(value: unknown) {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
}

export function mapLoadedAssignedUserLinks<TLink extends AssignedUserLink & { id?: number }, TKey extends string>(
  links: readonly TLink[],
  targetIdKey: TKey,
  getTargetId: (link: TLink) => unknown,
): Array<Record<TKey, number> & { id?: number; assignedUserIds: string[] }> {
  return links.flatMap((link) => {
    const targetId = parsePositiveInteger(getTargetId(link));
    if (targetId == null) {
      return [];
    }

    return [
      {
        id: link.id,
        [targetIdKey]: targetId,
        assignedUserIds: filterAssignedUserIds(link),
      } as Record<TKey, number> & { id?: number; assignedUserIds: string[] },
    ];
  });
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
  const parsedIds = ids?.flatMap((value) => {
    const id = parsePositiveInteger(value);
    return id == null ? [] : [id];
  });

  return [...new Set(parsedIds ?? [])].map((id) => ({
    [idKey]: id,
    assignedUserIds,
  })) as Array<Record<TKey, number> & { assignedUserIds: string[] }>;
}
