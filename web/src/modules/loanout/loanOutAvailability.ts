import type { LoanOutMetaData } from '@/api-access/generated/models';
import { DateTime } from 'luxon';

export function isLoanedOutOnDate(metadata: LoanOutMetaData[] | undefined, date: string, timeZone: string) {
  if (!metadata?.length) {
    return false;
  }

  const dayStart = DateTime.fromISO(date, { zone: timeZone }).startOf('day');
  const dayEnd = dayStart.plus({ days: 1 }).startOf('day');

  return metadata.some((item) => {
    const startAt = DateTime.fromISO(item.startAtUtc ?? '', { setZone: true });
    if (!startAt.isValid) {
      return false;
    }

    if (!item.endAtUtc) {
      return startAt >= dayStart && startAt < dayEnd;
    }

    const endAt = DateTime.fromISO(item.endAtUtc, { setZone: true });
    return endAt.isValid && startAt < dayEnd && endAt > dayStart;
  });
}
