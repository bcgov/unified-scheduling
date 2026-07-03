import { RRule } from 'rrule';
import type { ByWeekday, Options } from 'rrule';
import type { Frequency, NthWeekdayPosition, RRuleEditorModel, RRuleParseResult, Weekday } from './rruleEditor.types';
import { validateRRuleEditorModel } from './rruleEditor.validation';

const weekdayOrder: Weekday[] = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU'];

const frequencyToRRule: Record<Frequency, number> = {
  YEARLY: RRule.YEARLY,
  MONTHLY: RRule.MONTHLY,
  WEEKLY: RRule.WEEKLY,
  DAILY: RRule.DAILY,
};

const rruleToFrequency = new Map<number, Frequency>([
  [RRule.YEARLY, 'YEARLY'],
  [RRule.MONTHLY, 'MONTHLY'],
  [RRule.WEEKLY, 'WEEKLY'],
  [RRule.DAILY, 'DAILY'],
]);

const weekdayToRRule: Record<Weekday, ByWeekday> = {
  MO: RRule.MO,
  TU: RRule.TU,
  WE: RRule.WE,
  TH: RRule.TH,
  FR: RRule.FR,
  SA: RRule.SA,
  SU: RRule.SU,
};

const rruleWeekdayNumberToWeekday: Record<number, Weekday> = {
  0: 'MO',
  1: 'TU',
  2: 'WE',
  3: 'TH',
  4: 'FR',
  5: 'SA',
  6: 'SU',
};

const weekdayLabels: Record<Weekday, string> = {
  MO: 'Monday',
  TU: 'Tuesday',
  WE: 'Wednesday',
  TH: 'Thursday',
  FR: 'Friday',
  SA: 'Saturday',
  SU: 'Sunday',
};

const nthLabels: Record<NthWeekdayPosition, string> = {
  1: 'first',
  2: 'second',
  3: 'third',
  4: 'fourth',
  '-1': 'last',
};

export const weekdayOptions = weekdayOrder.map((weekday) => ({
  title: weekdayLabels[weekday],
  value: weekday,
}));

export function parseDateLike(value?: Date | string | null): Date {
  if (value instanceof Date && !Number.isNaN(value.valueOf())) {
    return new Date(value);
  }

  if (typeof value === 'string') {
    const dateOnly = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);

    if (dateOnly) {
      const [, year, month, day] = dateOnly;
      return new Date(Number(year), Number(month) - 1, Number(day));
    }

    const parsed = new Date(value);
    if (!Number.isNaN(parsed.valueOf())) {
      return parsed;
    }
  }

  return new Date();
}

export function getDefaultWeekday(startDate?: Date | string | null): Weekday {
  const jsDay = parseDateLike(startDate).getDay();
  return (['SU', 'MO', 'TU', 'WE', 'TH', 'FR', 'SA'] as Weekday[])[jsDay];
}

export function getDefaultRRuleEditorModel(
  startDate?: Date | string | null,
  untilDate?: Date | string | null,
): RRuleEditorModel {
  const date = parseDateLike(startDate);
  const weekday = getDefaultWeekday(date);
  const hasUntilDate = untilDate !== null && untilDate !== undefined && untilDate !== '';

  return {
    frequency: 'WEEKLY',
    interval: 1,
    weekdays: [weekday],
    monthlyMode: 'monthday',
    monthDay: date.getDate(),
    nthWeekday: {
      weekday,
      setPosition: getNthWeekdayPosition(date),
    },
    endMode: hasUntilDate ? 'until' : 'count',
    until: hasUntilDate ? parseDateLike(untilDate) : null,
    count: hasUntilDate ? null : 1,
  };
}

export function modelToRRuleString(model: RRuleEditorModel, startDate?: Date | string | null): string {
  const validation = validateRRuleEditorModel(model);

  if (!validation.valid) {
    throw new Error(validation.reason);
  }

  const start = parseDateLike(startDate);
  const options: Partial<Options> = {
    freq: frequencyToRRule[model.frequency],
    interval: model.interval,
  };

  if (model.frequency === 'WEEKLY') {
    const weekdays = model.weekdays.length > 0 ? model.weekdays : [getDefaultWeekday(start)];
    options.byweekday = weekdays.map((weekday) => weekdayToRRule[weekday]);
  }

  if (model.frequency === 'MONTHLY') {
    if (model.monthlyMode === 'monthday') {
      options.bymonthday = model.monthDay;
    } else if (model.nthWeekday) {
      options.byweekday = weekdayToRRule[model.nthWeekday.weekday];
      options.bysetpos = model.nthWeekday.setPosition;
    }
  }

  if (model.frequency === 'YEARLY') {
    options.bymonth = start.getMonth() + 1;
    options.bymonthday = start.getDate();
  }

  if (model.endMode === 'count') {
    options.count = getRRuleOccurrenceCount(model);
  }

  if (model.endMode === 'until' && model.until) {
    options.until = toInclusiveUtcUntil(model.until);
  }

  return new RRule(options).toString();
}

export function rruleStringToModel(value: string, startDate?: Date | string | null): RRuleParseResult {
  try {
    const rule = RRule.fromString(value);
    const options = rule.origOptions;
    const frequency = rruleToFrequency.get(options.freq ?? -1);

    if (!frequency) {
      return unsupported('Only daily, weekly, monthly, and yearly recurrence rules can be edited here.');
    }

    const unsupportedReason = findUnsupportedOptions(options, frequency);
    if (unsupportedReason) {
      return unsupported(unsupportedReason);
    }

    const model = getDefaultRRuleEditorModel(startDate);
    model.frequency = frequency;
    model.interval = options.interval ?? 1;
    const rruleCount = options.count ?? null;

    if (rruleCount != null) {
      model.endMode = 'count';
      model.until = null;
    } else if (options.until) {
      model.endMode = 'until';
      model.until = fromInclusiveUtcUntil(options.until);
      model.count = null;
    } else {
      model.endMode = 'never';
      model.until = null;
      model.count = null;
    }

    if (frequency === 'WEEKLY') {
      model.weekdays = toWeekdays(options.byweekday);
    }

    if (frequency === 'MONTHLY') {
      const monthDays = asArray(options.bymonthday);
      const bySetPos = asArray(options.bysetpos);
      const weekdays = toWeekdays(options.byweekday);

      if (monthDays.length === 1) {
        model.monthlyMode = 'monthday';
        model.monthDay = monthDays[0];
      } else if (bySetPos.length === 1 && weekdays.length === 1) {
        model.monthlyMode = 'nth-weekday';
        model.nthWeekday = {
          weekday: weekdays[0],
          setPosition: bySetPos[0] as NthWeekdayPosition,
        };
      }
    }

    if (rruleCount != null) {
      model.count = getEditorRecurrenceCount(rruleCount, model);
    }

    return { supported: true, model };
  } catch {
    return unsupported('This recurrence rule could not be parsed.');
  }
}

export function getRRulePreview(model: RRuleEditorModel, startDate?: Date | string | null): string {
  const validation = validateRRuleEditorModel(model);

  if (!validation.valid) {
    return validation.reason ?? 'This recurrence rule is incomplete.';
  }

  const interval = model.interval;
  const frequencyLabel = getFrequencyLabel(model.frequency, interval);
  const parts = [`Repeats every ${interval === 1 ? frequencyLabel.singular : `${interval} ${frequencyLabel.plural}`}`];

  if (model.frequency === 'WEEKLY') {
    parts.push(`on ${formatList(model.weekdays.map((weekday) => weekdayLabels[weekday]))}`);
  }

  if (model.frequency === 'MONTHLY') {
    if (model.monthlyMode === 'monthday') {
      parts.push(`on day ${model.monthDay}`);
    } else if (model.nthWeekday) {
      parts.push(`on the ${nthLabels[model.nthWeekday.setPosition]} ${weekdayLabels[model.nthWeekday.weekday]}`);
    }
  }

  if (model.frequency === 'YEARLY') {
    parts.push(`on ${formatMonthDay(parseDateLike(startDate))}`);
  }

  if (model.endMode === 'count') {
    parts.push(`ends after ${model.count} ${model.count === 1 ? 'recurrence' : 'recurrences'}`);
  }

  if (model.endMode === 'until' && model.until) {
    parts.push(`ends on ${formatDate(model.until)}`);
  }

  return parts.join(', ');
}

function getFrequencyLabel(frequency: Frequency, interval: number) {
  const labels: Record<Frequency, { singular: string; plural: string }> = {
    DAILY: { singular: 'day', plural: 'days' },
    WEEKLY: { singular: 'week', plural: 'weeks' },
    MONTHLY: { singular: 'month', plural: 'months' },
    YEARLY: { singular: 'year', plural: 'years' },
  };

  return interval === 1 ? labels[frequency] : labels[frequency];
}

function getRRuleOccurrenceCount(model: RRuleEditorModel): number | null | undefined {
  if (model.count == null) {
    return model.count;
  }

  return model.count * getOccurrencesPerRecurrence(model);
}

function getEditorRecurrenceCount(rruleCount: number, model: RRuleEditorModel): number {
  return Math.ceil(rruleCount / getOccurrencesPerRecurrence(model));
}

function getOccurrencesPerRecurrence(model: RRuleEditorModel): number {
  if (model.frequency === 'WEEKLY') {
    return Math.max(model.weekdays.length, 1);
  }

  return 1;
}

function findUnsupportedOptions(options: Partial<Options>, frequency: Frequency): string | null {
  if (options.dtstart) {
    return 'Rules with DTSTART cannot be edited here.';
  }

  if (options.wkst || options.tzid || options.byhour || options.byminute || options.bysecond || options.byyearday) {
    return 'This recurrence rule uses advanced options that cannot be edited here.';
  }

  if (options.byweekno || options.byeaster || options.bynmonthday || options.bynweekday) {
    return 'This recurrence rule uses advanced options that cannot be edited here.';
  }

  if (options.count != null && options.until) {
    return 'Rules with both COUNT and UNTIL cannot be edited here.';
  }

  const bymonth = asArray(options.bymonth);
  const bymonthday = asArray(options.bymonthday);
  const bysetpos = asArray(options.bysetpos);
  const byweekday = asArray(options.byweekday);

  if (bymonth.length > 1 || bysetpos.length > 1) {
    return 'This recurrence rule uses advanced options that cannot be edited here.';
  }

  if (!bysetpos.every(isValidNthWeekdayPosition)) {
    return 'This recurrence rule uses an unsupported monthly position.';
  }

  if (frequency === 'DAILY' && (bymonth.length || bymonthday.length || bysetpos.length || byweekday.length)) {
    return 'Daily rules with additional filters cannot be edited here.';
  }

  if (frequency === 'WEEKLY' && (bymonth.length || bymonthday.length || bysetpos.length)) {
    return 'Weekly rules with additional filters cannot be edited here.';
  }

  if (frequency === 'MONTHLY') {
    const isMonthDay = bymonthday.length === 1 && byweekday.length === 0 && bysetpos.length === 0;
    const isNthWeekday = bymonthday.length === 0 && byweekday.length === 1 && bysetpos.length === 1;

    if (!isMonthDay && !isNthWeekday) {
      return 'Only monthly day-of-month and nth-weekday rules can be edited here.';
    }
  }

  if (frequency === 'YEARLY') {
    if (bymonth.length !== 1 || bymonthday.length !== 1 || byweekday.length || bysetpos.length) {
      return 'Only yearly rules on a single month and day can be edited here.';
    }
  }

  return null;
}

function toWeekdays(value: Partial<Options>['byweekday']): Weekday[] {
  return asArray(value)
    .map((weekday) => {
      if (typeof weekday === 'number') {
        return rruleWeekdayNumberToWeekday[weekday];
      }

      if (typeof weekday === 'string') {
        return weekday as Weekday;
      }

      return rruleWeekdayNumberToWeekday[weekday.weekday];
    })
    .filter(Boolean);
}

function getNthWeekdayPosition(date: Date): NthWeekdayPosition {
  const dayOfMonth = date.getDate();
  const position = Math.ceil(dayOfMonth / 7);
  const nextWeekSameWeekday = new Date(date);
  nextWeekSameWeekday.setDate(dayOfMonth + 7);

  if (nextWeekSameWeekday.getMonth() !== date.getMonth()) {
    return -1;
  }

  return Math.min(position, 4) as NthWeekdayPosition;
}

function toInclusiveUtcUntil(date: Date): Date {
  return new Date(Date.UTC(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59));
}

function fromInclusiveUtcUntil(date: Date): Date {
  return new Date(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate());
}

function formatList(values: string[]): string {
  if (values.length <= 2) {
    return values.join(values.length === 2 ? ' and ' : '');
  }

  return `${values.slice(0, -1).join(', ')} and ${values.at(-1)}`;
}

function formatMonthDay(date: Date): string {
  return date.toLocaleDateString(undefined, { month: 'long', day: 'numeric' });
}

function formatDate(date: Date): string {
  return date.toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' });
}

function unsupported(reason: string): RRuleParseResult {
  return { supported: false, reason };
}

function asArray<T>(value: T | T[] | null | undefined): T[] {
  if (value == null) {
    return [];
  }

  return Array.isArray(value) ? value : [value];
}

function isValidNthWeekdayPosition(value: number): value is NthWeekdayPosition {
  return value === 1 || value === 2 || value === 3 || value === 4 || value === -1;
}
