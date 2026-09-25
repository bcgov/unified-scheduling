export type SchedulingLifecycleStatus = 'draft' | 'published' | 'cancelled' | 'unknown';

export interface SchedulingLifecycleCapabilities {
  status: SchedulingLifecycleStatus;
  canEdit: boolean;
  canDelete: boolean;
  canCancel: boolean;
}

export function normalizeSchedulingLifecycleStatus(statusTypeCode?: string | null): SchedulingLifecycleStatus {
  switch (statusTypeCode?.trim().toLowerCase()) {
    case 'draft':
      return 'draft';
    case 'active':
    case 'published':
      return 'published';
    case 'cancelled':
    case 'canceled':
      return 'cancelled';
    default:
      return 'unknown';
  }
}

export function getSchedulingLifecycleCapabilities(statusTypeCode?: string | null): SchedulingLifecycleCapabilities {
  const status = normalizeSchedulingLifecycleStatus(statusTypeCode);

  return {
    status,
    canEdit: status === 'draft',
    canDelete: status === 'draft',
    canCancel: status === 'published',
  };
}

export function canAddAssignmentLinkToShift(statusTypeCode?: string | null) {
  return normalizeSchedulingLifecycleStatus(statusTypeCode) === 'draft';
}

export function isSchedulingCancelled(statusTypeCode?: string | null) {
  return normalizeSchedulingLifecycleStatus(statusTypeCode) === 'cancelled';
}

export function isSchedulingPublished(statusTypeCode?: string | null) {
  return normalizeSchedulingLifecycleStatus(statusTypeCode) === 'published';
}

export function isSchedulingLinkable(statusTypeCode?: string | null) {
  const status = normalizeSchedulingLifecycleStatus(statusTypeCode);
  return status === 'draft' || status === 'published';
}
