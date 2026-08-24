import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { AssignmentSeriesResponse } from '@/api-access/generated/models/assignmentSeriesResponse';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import { computed, ref, watch, type ComputedRef, type Ref } from 'vue';
import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';
import * as assignmentApi from './calendarSchedulingAssignmentApi';

const MAX_SERIES_OCCURRENCES = 400;
const weekdayByRRuleCode: Record<string, number> = {
  MO: 1,
  TU: 2,
  WE: 3,
  TH: 4,
  FR: 5,
  SA: 6,
  SU: 7,
};

interface ShiftOccurrence {
  start: DateTime;
  end: DateTime;
  dateKey: string;
}

export function useSchedulingAssignmentOptions(options: {
  formData: Ref<ShiftResourceFormData>;
  activeLocationId: ComputedRef<number | null>;
  activeTimeZoneId: ComputedRef<string>;
  isSeriesScope: ComputedRef<boolean>;
  onError: (message: string) => void;
}) {
  const assignmentEntries = ref<AssignmentEntryResponse[]>([]);
  const assignmentSeries = ref<AssignmentSeriesResponse[]>([]);
  const isLoadingAssignments = ref(false);
  const assignmentWarning = ref('');

  const assignmentEntryOptions = computed<SelectOption[]>(() =>
    assignmentEntries.value.filter(hasNumericId).map((entry) => ({
      code: entry.id!,
      description: formatAssignmentEntryLabel(entry, options.activeTimeZoneId.value),
    })),
  );

  const assignmentSeriesOptions = computed<SelectOption[]>(() =>
    assignmentSeries.value.filter(hasNumericId).map((series) => ({
      code: series.id!,
      description: formatAssignmentSeriesLabel(series, options.activeTimeZoneId.value),
    })),
  );

  watch(
    [
      () => options.formData.value.date,
      () => options.formData.value.startTime,
      () => options.formData.value.endTime,
      () => options.formData.value.recurrenceRule,
      () => options.formData.value.repeatMode,
      () => options.activeLocationId.value,
      () => options.activeTimeZoneId.value,
      () => options.isSeriesScope.value,
    ],
    () => {
      void loadOptions();
    },
    { immediate: true },
  );

  async function loadOptions() {
    const locationId = options.activeLocationId.value;
    const occurrences = buildShiftOccurrences(
      options.formData.value,
      options.activeTimeZoneId.value,
      options.isSeriesScope.value,
    );

    assignmentEntries.value = [];
    assignmentSeries.value = [];
    assignmentWarning.value = '';

    if (!locationId || occurrences.length === 0) {
      return;
    }

    isLoadingAssignments.value = true;

    try {
      const entryRange = buildWholeDayRange([occurrences[0]], options.activeTimeZoneId.value);
      if (entryRange) {
        const entryResult = await assignmentApi.loadAssignmentEntries({
          LocationId: locationId,
          StartAtUtc: entryRange.startAtUtc,
          EndAtUtc: entryRange.endAtUtc,
        });

        if (entryResult.error.value) {
          options.onError(entryResult.error.value.message || 'Failed to load assignments.');
        } else {
          assignmentEntries.value = (entryResult.data.value ?? []).filter(isLinkableStatus);
        }
      }

      if (options.isSeriesScope.value) {
        const seriesRange = buildWholeDayRange(occurrences, options.activeTimeZoneId.value);
        if (seriesRange) {
          const seriesResult = await assignmentApi.loadAssignmentSeries({
            LocationId: locationId,
            StartAtUtc: seriesRange.startAtUtc,
            EndAtUtc: seriesRange.endAtUtc,
          });

          if (seriesResult.error.value) {
            options.onError(seriesResult.error.value.message || 'Failed to load assignment series.');
          } else {
            assignmentSeries.value = filterSeriesByMatchingActiveEntryDate(
              (seriesResult.data.value ?? []).filter(isLinkableStatus),
              occurrences,
              options.activeTimeZoneId.value,
            );
          }
        }
      }

      assignmentWarning.value = buildNoTimeOverlapWarning(
        assignmentEntries.value,
        assignmentSeries.value,
        occurrences,
        options.activeTimeZoneId.value,
      );
    } finally {
      isLoadingAssignments.value = false;
    }
  }

  return {
    assignmentEntryOptions,
    assignmentSeriesOptions,
    assignmentWarning,
    isLoadingAssignments,
    loadOptions,
  };
}

function buildShiftOccurrences(
  formData: ShiftResourceFormData,
  timeZoneId: string,
  isSeriesScope: boolean,
): ShiftOccurrence[] {
  const first = buildOccurrence(formData.date, formData.startTime, formData.endTime, timeZoneId);
  if (!first) {
    return [];
  }

  if (!isSeriesScope || !formData.recurrenceRule) {
    return [first];
  }

  return expandSimpleRRule(first, formData.recurrenceRule);
}

function buildOccurrence(
  date: string | undefined,
  startTime: string | undefined,
  endTime: string | undefined,
  timeZoneId: string,
) {
  if (!date || !startTime || !endTime) {
    return null;
  }

  const start = DateTime.fromISO(`${date}T${startTime}`, { zone: timeZoneId });
  const end = DateTime.fromISO(`${date}T${endTime}`, { zone: timeZoneId });
  if (!start.isValid || !end.isValid || end <= start) {
    return null;
  }

  return {
    start,
    end,
    dateKey: start.toISODate() ?? '',
  };
}

function expandSimpleRRule(first: ShiftOccurrence, recurrenceRule: string): ShiftOccurrence[] {
  const parts = parseRRuleParts(recurrenceRule);
  const frequency = parts.FREQ;
  const interval = Math.max(Number(parts.INTERVAL ?? '1'), 1);
  const count = Math.min(Number(parts.COUNT ?? '0') || MAX_SERIES_OCCURRENCES, MAX_SERIES_OCCURRENCES);
  const until = parts.UNTIL ? parseUntil(parts.UNTIL, first.start.zoneName ?? 'local') : null;
  const duration = first.end.diff(first.start);
  const occurrences: ShiftOccurrence[] = [];

  if (!frequency) {
    return [first];
  }

  let cursor = first.start;
  let guard = 0;
  while (occurrences.length < count && guard < MAX_SERIES_OCCURRENCES * 10) {
    guard += 1;

    if (until && cursor.startOf('day') > until.startOf('day')) {
      break;
    }

    if (matchesRRuleDate(cursor, first.start, frequency, interval, parts)) {
      occurrences.push({
        start: cursor,
        end: cursor.plus(duration),
        dateKey: cursor.toISODate() ?? '',
      });
    }

    cursor = cursor.plus({ days: 1 });
  }

  return occurrences.length ? occurrences : [first];
}

function parseRRuleParts(value: string) {
  const text = value.replace(/^RRULE:/i, '');
  return Object.fromEntries(
    text
      .split(';')
      .map((part) => part.split('='))
      .filter((part): part is [string, string] => part.length === 2 && Boolean(part[0])),
  );
}

function parseUntil(value: string, timeZoneId: string) {
  const dateOnly = /^(\d{4})(\d{2})(\d{2})/.exec(value);
  if (!dateOnly) {
    return null;
  }

  const [, year, month, day] = dateOnly;
  return DateTime.fromObject({ year: Number(year), month: Number(month), day: Number(day) }, { zone: timeZoneId });
}

function matchesRRuleDate(
  cursor: DateTime,
  first: DateTime,
  frequency: string,
  interval: number,
  parts: Record<string, string>,
) {
  const daysSinceStart = Math.floor(cursor.startOf('day').diff(first.startOf('day'), 'days').days);
  if (daysSinceStart < 0) {
    return false;
  }

  if (frequency === 'DAILY') {
    return daysSinceStart % interval === 0;
  }

  if (frequency === 'WEEKLY') {
    const weekNumber = Math.floor(daysSinceStart / 7);
    const weekdays = (parts.BYDAY?.split(',') ?? []).map((day) => weekdayByRRuleCode[day]).filter(Boolean);
    const matchesWeekday = weekdays.length ? weekdays.includes(cursor.weekday) : cursor.weekday === first.weekday;
    return weekNumber % interval === 0 && matchesWeekday;
  }

  if (frequency === 'MONTHLY') {
    const monthsSinceStart = (cursor.year - first.year) * 12 + cursor.month - first.month;
    const monthDay = Number(parts.BYMONTHDAY ?? first.day);
    return monthsSinceStart >= 0 && monthsSinceStart % interval === 0 && cursor.day === monthDay;
  }

  if (frequency === 'YEARLY') {
    const yearsSinceStart = cursor.year - first.year;
    return (
      yearsSinceStart >= 0 &&
      yearsSinceStart % interval === 0 &&
      cursor.month === first.month &&
      cursor.day === first.day
    );
  }

  return cursor.hasSame(first, 'day');
}

function buildWholeDayRange(occurrences: ShiftOccurrence[], timeZoneId: string) {
  const dateKeys = occurrences
    .map((occurrence) => occurrence.dateKey)
    .filter(Boolean)
    .sort();
  const firstDate = dateKeys[0];
  const lastDate = dateKeys.at(-1);
  if (!firstDate || !lastDate) {
    return null;
  }

  return {
    startAtUtc:
      DateTime.fromISO(firstDate, { zone: timeZoneId }).startOf('day').toUTC().toISO({ suppressMilliseconds: true }) ??
      '',
    endAtUtc:
      DateTime.fromISO(lastDate, { zone: timeZoneId })
        .plus({ days: 1 })
        .startOf('day')
        .toUTC()
        .toISO({ suppressMilliseconds: true }) ?? '',
  };
}

function filterSeriesByMatchingActiveEntryDate(
  series: AssignmentSeriesResponse[],
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  return series.filter((item) =>
    (item.entries ?? []).some(
      (entry) =>
        isLinkableStatus(entry) &&
        occurrences.some((occurrence) => entryOverlapsShiftDate(entry, occurrence, timeZoneId)),
    ),
  );
}

function buildNoTimeOverlapWarning(
  entries: AssignmentEntryResponse[],
  series: AssignmentSeriesResponse[],
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  const hasEntryDateOnlyMatch = entries.some((entry) => hasDateMatchWithoutTimeOverlap(entry, occurrences, timeZoneId));
  const hasSeriesDateOnlyMatch = series.some((item) =>
    (item.entries ?? []).some(
      (entry) => isLinkableStatus(entry) && hasDateMatchWithoutTimeOverlap(entry, occurrences, timeZoneId),
    ),
  );

  if (!hasEntryDateOnlyMatch && !hasSeriesDateOnlyMatch) {
    return '';
  }

  return 'One or more matching assignments occur on the same day but do not overlap this shift time.';
}

function hasDateMatchWithoutTimeOverlap(
  entry: AssignmentEntryResponse,
  occurrences: ShiftOccurrence[],
  timeZoneId: string,
) {
  const interval = getEntryInterval(entry, timeZoneId);
  if (!interval) {
    return false;
  }

  const dateMatches = occurrences.filter((occurrence) =>
    intervalsOverlap(
      occurrence.start.startOf('day'),
      occurrence.start.plus({ days: 1 }).startOf('day'),
      interval.start,
      interval.end,
    ),
  );
  return (
    dateMatches.length > 0 &&
    dateMatches.every((occurrence) => !intervalsOverlap(occurrence.start, occurrence.end, interval.start, interval.end))
  );
}

function entryOverlapsShiftDate(entry: AssignmentEntryResponse, occurrence: ShiftOccurrence, timeZoneId: string) {
  const interval = getEntryInterval(entry, timeZoneId);
  if (!interval) {
    return false;
  }

  return intervalsOverlap(
    occurrence.start.startOf('day'),
    occurrence.start.plus({ days: 1 }).startOf('day'),
    interval.start,
    interval.end,
  );
}

function getEntryInterval(entry: AssignmentEntryResponse, timeZoneId: string) {
  if (!entry.startAtUtc) {
    return null;
  }

  const start = DateTime.fromISO(entry.startAtUtc, { zone: timeZoneId });
  const end = entry.endAtUtc ? DateTime.fromISO(entry.endAtUtc, { zone: timeZoneId }) : start;
  if (!start.isValid || !end.isValid) {
    return null;
  }

  return { start, end };
}

function intervalsOverlap(leftStart: DateTime, leftEnd: DateTime, rightStart: DateTime, rightEnd: DateTime) {
  return leftStart < rightEnd && rightStart < leftEnd;
}

function isLinkableStatus(item: { statusTypeCode?: string | null }) {
  const normalized = String(item.statusTypeCode ?? '').toLowerCase();
  return normalized === 'active' || normalized === 'draft';
}

function hasNumericId(item: { id?: number }): item is { id: number } {
  return typeof item.id === 'number';
}

function formatAssignmentEntryLabel(entry: AssignmentEntryResponse, timeZoneId: string) {
  const title = entry.title?.trim() || `Assignment ${entry.id}`;
  const start = entry.startAtUtc ? DateTime.fromISO(entry.startAtUtc, { zone: timeZoneId }) : null;
  const end = entry.endAtUtc ? DateTime.fromISO(entry.endAtUtc, { zone: timeZoneId }) : null;
  if (!start?.isValid) {
    return title;
  }

  const time = end?.isValid
    ? `${start.toFormat('MMM d, h:mm a')} - ${end.toFormat('h:mm a')}`
    : start.toFormat('MMM d, h:mm a');
  return `${title} (${time})`;
}

function formatAssignmentSeriesLabel(series: AssignmentSeriesResponse, timeZoneId: string) {
  const title = series.title?.trim() || `Assignment series ${series.id}`;
  const start = series.startAtUtc ? DateTime.fromISO(series.startAtUtc, { zone: timeZoneId }) : null;
  const end = series.endAtUtc ? DateTime.fromISO(series.endAtUtc, { zone: timeZoneId }) : null;
  if (!start?.isValid) {
    return title;
  }

  const time = end?.isValid
    ? `${start.toFormat('MMM d, h:mm a')} - ${end.toFormat('h:mm a')}`
    : start.toFormat('MMM d, h:mm a');
  return `${title} (${time})`;
}
