import { DateTime } from 'luxon';
import type { AuditRecordResponseDto } from '@/api-access/generated/models';

export const EMPTY_VALUE_DISPLAY = '—';

// Matches the ISO 8601 timestamps .NET emits for DateTime/DateTimeOffset properties (e.g. CreatedOn/UpdatedOn).
const ISO_DATE_PATTERN = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/;

export function formatOccurredOn(occurredOn: string): string {
  const dt = DateTime.fromISO(occurredOn);
  return dt.isValid ? dt.toLocal().toFormat('yyyy-MM-dd HH:mm:ss') : occurredOn;
}

export function formatDiffValue(value: unknown): string {
  if (value === null || value === undefined || value === '') {
    return EMPTY_VALUE_DISPLAY;
  }
  if (typeof value === 'boolean') {
    return value ? 'true' : 'false';
  }
  if (typeof value === 'string' && ISO_DATE_PATTERN.test(value)) {
    return formatOccurredOn(value);
  }
  if (typeof value === 'object') {
    return JSON.stringify(value);
  }
  return String(value);
}

export function formatKeyValues(keyValues: unknown): string {
  if (!keyValues || typeof keyValues !== 'object') {
    return EMPTY_VALUE_DISPLAY;
  }
  const entries = Object.entries(keyValues as Record<string, unknown>);
  if (entries.length === 0) {
    return EMPTY_VALUE_DISPLAY;
  }
  return entries.map(([key, value]) => `${key}: ${formatDiffValue(value)}`).join(', ');
}

export type AuditDiffRow = {
  field: string;
  label: string;
  before: string;
  after: string;
};

/**
 * Builds the before/after comparison rows for the expandable diff panel. Added records have no
 * "before" state and Deleted records have no "after" state, so every field on the record is shown.
 */
export function buildDiffRows(
  record: Pick<AuditRecordResponseDto, 'action' | 'oldValues' | 'newValues' | 'changedColumns'>,
  labelByField: Map<string, string>,
): AuditDiffRow[] {
  const oldValues = (record.oldValues ?? {}) as Record<string, unknown>;
  const newValues = (record.newValues ?? {}) as Record<string, unknown>;
  const labelFor = (field: string) => labelByField.get(field) ?? field;

  if (record.action === 'Added') {
    return Object.keys(newValues)
      .sort()
      .map((field) => ({
        field,
        label: labelFor(field),
        before: EMPTY_VALUE_DISPLAY,
        after: formatDiffValue(newValues[field]),
      }));
  }

  if (record.action === 'Deleted') {
    return Object.keys(oldValues)
      .sort()
      .map((field) => ({
        field,
        label: labelFor(field),
        before: formatDiffValue(oldValues[field]),
        after: EMPTY_VALUE_DISPLAY,
      }));
  }

  return (record.changedColumns ?? []).map((field) => ({
    field,
    label: labelFor(field),
    before: formatDiffValue(oldValues[field]),
    after: formatDiffValue(newValues[field]),
  }));
}
