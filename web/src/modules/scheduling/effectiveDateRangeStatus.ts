import type { LookupCodeResponse } from '@/api-access/generated/models';
import { toDateTime } from '@/utils/date';
import { DateTime } from 'luxon';

export function isEffectiveDateRangeActive(
  item: Pick<LookupCodeResponse, 'effectiveDate' | 'expiryDate'>,
  now: DateTime = DateTime.utc(),
) {
  const effectiveDate = item.effectiveDate ? toDateTime(item.effectiveDate) : null;
  const expiryDate = item.expiryDate ? toDateTime(item.expiryDate) : null;

  if (effectiveDate?.isValid && effectiveDate > now) {
    return false;
  }

  if (expiryDate?.isValid && expiryDate <= now) {
    return false;
  }

  return true;
}
