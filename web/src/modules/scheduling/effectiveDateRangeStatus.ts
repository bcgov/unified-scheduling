import type { LookupCodeResponse } from '@/api-access/generated/models';
import { DateTime } from 'luxon';

export function isEffectiveDateRangeActive(
  item: Pick<LookupCodeResponse, 'effectiveDate' | 'expiryDate'>,
  now: DateTime = DateTime.utc(),
) {
  const effectiveDate = toDateTime(item.effectiveDate);
  const expiryDate = toDateTime(item.expiryDate);

  if (effectiveDate?.isValid && effectiveDate > now) {
    return false;
  }

  if (expiryDate?.isValid && expiryDate <= now) {
    return false;
  }

  return true;
}

function toDateTime(value?: string | null) {
  if (!value) {
    return null;
  }

  return DateTime.fromISO(value);
}
