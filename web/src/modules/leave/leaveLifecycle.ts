export function isDraftLeave(statusTypeCode?: string | null) {
  return statusTypeCode?.toLowerCase() === 'draft';
}

export function isActiveLeave(statusTypeCode?: string | null) {
  return statusTypeCode?.toLowerCase() === 'active';
}

export function isLeaveEditable(statusTypeCode?: string | null) {
  return isDraftLeave(statusTypeCode) || isActiveLeave(statusTypeCode);
}
