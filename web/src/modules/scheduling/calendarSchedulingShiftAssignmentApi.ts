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

export async function syncAssignmentEntryLinks(
  assignmentEntryId: number,
  desiredLinks: DesiredShiftEntryLink[],
  existingLinkIds: number[],
) {
  const retainedIds = new Set(desiredLinks.flatMap((link) => (link.id ? [link.id] : [])));

  for (const linkId of existingLinkIds.filter((id) => !retainedIds.has(id))) {
    await executeLinkMutation(deleteApiSchedulingShiftAssignmentsEntriesId(linkId, deferredOptions));
  }

  for (const link of desiredLinks) {
    const mutation = link.id
      ? putApiSchedulingShiftAssignmentsEntriesId(
          link.id,
          { userIds: link.assignedUserIds },
          deferredOptions,
        )
      : postApiSchedulingShiftAssignmentsEntries(
          {
            shiftEntryId: link.shiftEntryId,
            assignmentEntryId,
            userIds: link.assignedUserIds,
          },
          deferredOptions,
        );
    await executeLinkMutation(mutation);
  }
}

export async function syncAssignmentSeriesLinks(
  assignmentSeriesId: number,
  desiredLinks: DesiredShiftSeriesLink[],
  existingLinkIds: number[],
) {
  const retainedIds = new Set(desiredLinks.flatMap((link) => (link.id ? [link.id] : [])));

  for (const linkId of existingLinkIds.filter((id) => !retainedIds.has(id))) {
    await executeLinkMutation(deleteApiSchedulingShiftAssignmentsSeriesId(linkId, deferredOptions));
  }

  for (const link of desiredLinks) {
    const mutation = link.id
      ? putApiSchedulingShiftAssignmentsSeriesId(
          link.id,
          { assignedUserIds: link.assignedUserIds },
          deferredOptions,
        )
      : postApiSchedulingShiftAssignmentsSeries(
          {
            shiftSeriesId: link.shiftSeriesId,
            assignmentSeriesId,
            assignedUserIds: link.assignedUserIds,
          },
          deferredOptions,
        );
    await executeLinkMutation(mutation);
  }
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
