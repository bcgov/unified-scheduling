import { DateTime, type Duration } from 'luxon';
import { RRule } from 'rrule';

export interface SchedulingOccurrence {
  start: DateTime;
  end: DateTime;
  dateKey: string;
}

const maxOccurrences = 400;

export function expandSchedulingRecurrence(
  firstStart: DateTime,
  duration: Duration,
  recurrenceRule: string,
): SchedulingOccurrence[] {
  try {
    const options = RRule.parseString(recurrenceRule.replace(/^RRULE:/i, ''));
    options.dtstart = toFloatingDate(firstStart);
    const rule = new RRule(options);
    const dates = rule.all((_date, index) => index < maxOccurrences);

    return dates.map((date) => {
      const start = fromFloatingDate(date, firstStart.zoneName ?? 'UTC');
      return {
        start,
        end: start.plus(duration),
        dateKey: start.toISODate() ?? '',
      };
    });
  } catch {
    return [];
  }
}

function toFloatingDate(value: DateTime) {
  return new Date(
    Date.UTC(value.year, value.month - 1, value.day, value.hour, value.minute, value.second, value.millisecond),
  );
}

function fromFloatingDate(value: Date, timeZoneId: string) {
  return DateTime.fromObject(
    {
      year: value.getUTCFullYear(),
      month: value.getUTCMonth() + 1,
      day: value.getUTCDate(),
      hour: value.getUTCHours(),
      minute: value.getUTCMinutes(),
      second: value.getUTCSeconds(),
      millisecond: value.getUTCMilliseconds(),
    },
    { zone: timeZoneId },
  );
}
