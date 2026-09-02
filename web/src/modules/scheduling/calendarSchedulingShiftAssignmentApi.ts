import {
  deleteApiSchedulingShiftAssignmentsEntriesId,
  deleteApiSchedulingShiftAssignmentsSeriesId,
  postApiSchedulingShiftAssignmentsEntries,
  postApiSchedulingShiftAssignmentsSeries,
  putApiSchedulingShiftAssignmentsEntriesId,
  putApiSchedulingShiftAssignmentsSeriesId,
} from '@/api-access/generated/shift-assignment/shift-assignment';

export interface DesiredShiftEntryLink {
  id?: number;
  shiftEntryId: number;
  assignedUserIds: string[];
}

export interface DesiredShiftSeriesLink {
  id?: number;
  shiftSeriesId: number;
  assignedUserIds: string[];
}

export interface ExistingShiftEntryLink extends DesiredShiftEntryLink {
  id: number;
}

export interface ExistingShiftSeriesLink extends DesiredShiftSeriesLink {
  id: number;
}

export interface RelationshipDelta<TLink> {
  updates: Array<TLink & { id: number }>;
  creates: TLink[];
  deletes: number[];
}

export async function syncAssignmentEntryLinks(
  assignmentEntryId: number,
  desiredLinks: DesiredShiftEntryLink[],
  existingLinks: ExistingShiftEntryLink[],
) {
  const delta = calculateRelationshipDelta(desiredLinks, existingLinks, (link) => link.shiftEntryId);
  await applyRelationshipDelta(delta, {
    update: (link) =>
      executeLinkMutation(
        putApiSchedulingShiftAssignmentsEntriesId(link.id, { userIds: link.assignedUserIds }, deferredOptions),
      ),
    create: (link) =>
      executeLinkMutation(
        postApiSchedulingShiftAssignmentsEntries(
          { shiftEntryId: link.shiftEntryId, assignmentEntryId, userIds: link.assignedUserIds },
          deferredOptions,
        ),
      ),
    delete: (linkId) => executeLinkMutation(deleteApiSchedulingShiftAssignmentsEntriesId(linkId, deferredOptions)),
  });
}

export async function syncAssignmentSeriesLinks(
  assignmentSeriesId: number,
  desiredLinks: DesiredShiftSeriesLink[],
  existingLinks: ExistingShiftSeriesLink[],
) {
  const delta = calculateRelationshipDelta(desiredLinks, existingLinks, (link) => link.shiftSeriesId);
  await applyRelationshipDelta(delta, {
    update: (link) =>
      executeLinkMutation(
        putApiSchedulingShiftAssignmentsSeriesId(link.id, { assignedUserIds: link.assignedUserIds }, deferredOptions),
      ),
    create: (link) =>
      executeLinkMutation(
        postApiSchedulingShiftAssignmentsSeries(
          { shiftSeriesId: link.shiftSeriesId, assignmentSeriesId, assignedUserIds: link.assignedUserIds },
          deferredOptions,
        ),
      ),
    delete: (linkId) => executeLinkMutation(deleteApiSchedulingShiftAssignmentsSeriesId(linkId, deferredOptions)),
  });
}

export function calculateRelationshipDelta<TLink extends { assignedUserIds: string[] }>(
  desiredLinks: TLink[],
  existingLinks: Array<TLink & { id: number }>,
  getTargetId: (link: TLink) => number,
): RelationshipDelta<TLink> {
  assertUniqueTargets(desiredLinks, getTargetId);
  const existingByTargetId = new Map(existingLinks.map((link) => [getTargetId(link), link]));
  const retainedIds = new Set<number>();
  const updates: Array<TLink & { id: number }> = [];
  const creates: TLink[] = [];

  for (const desired of desiredLinks) {
    const existing = existingByTargetId.get(getTargetId(desired));
    if (!existing) {
      creates.push(desired);
      continue;
    }

    retainedIds.add(existing.id);
    if (!setsEqual(desired.assignedUserIds, existing.assignedUserIds)) {
      updates.push({ ...desired, id: existing.id });
    }
  }

  return {
    updates,
    creates,
    deletes: existingLinks.filter((link) => !retainedIds.has(link.id)).map((link) => link.id),
  };
}

async function applyRelationshipDelta<TLink>(
  delta: RelationshipDelta<TLink>,
  mutations: {
    update: (link: TLink & { id: number }) => Promise<void>;
    create: (link: TLink) => Promise<void>;
    delete: (id: number) => Promise<void>;
  },
) {
  // Sequential awaits deliberately stop all remaining mutations after the first failure.
  for (const link of delta.updates) await mutations.update(link);
  for (const link of delta.creates) await mutations.create(link);
  for (const id of delta.deletes) await mutations.delete(id);
}

function assertUniqueTargets<TLink>(desiredLinks: TLink[], getTargetId: (link: TLink) => number) {
  const targetIds = new Set<number>();
  for (const link of desiredLinks) {
    const targetId = getTargetId(link);
    if (targetIds.has(targetId)) {
      throw new Error('The same Shift and Assignment cannot be linked more than once.');
    }
    targetIds.add(targetId);
  }
}

function setsEqual(left: string[], right: string[]) {
  return left.length === right.length && left.every((value) => right.includes(value));
}

const deferredOptions = { options: { immediate: false } } as const;

async function executeLinkMutation(mutation: {
  execute: () => Promise<unknown>;
  error: { value: { message?: string } | null };
}) {
  await mutation.execute();
  if (mutation.error.value) {
    throw new Error(mutation.error.value.message || 'Failed to save assignment links.');
  }
}
