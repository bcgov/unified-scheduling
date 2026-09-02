import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';

export const timeOptions = buildTimeOptions();
export const defaultStartTime = buildTimeOptionValue(9, 0);
export const defaultEndTime = buildTimeOptionValue(17, 0);

export function buildLocalDateTime(date?: string, time?: string, timeZone?: string) {
  if (!date || !time) {
    return null;
  }

  return DateTime.fromISO(`${date}T${time}`, { zone: timeZone });
}

export function buildLocalDateTimeRange(date?: string, startTime?: string, endTime?: string, timeZone?: string) {
  const start = buildLocalDateTime(date, startTime, timeZone);
  const end = buildLocalDateTime(date, endTime, timeZone);

  if (!start?.isValid || !end?.isValid || end <= start) {
    return null;
  }

  return { start, end };
}

export function toUtcIso(date?: string, time?: string, timeZone?: string) {
  const dateTime = buildLocalDateTime(date, time, timeZone);
  if (!dateTime?.isValid) {
    return null;
  }

  return dateTime.toUTC().toISO({ suppressMilliseconds: true });
}

export function toFormDateTime(value: string, timeZoneId: string) {
  return parseFormDateTime(value, timeZoneId) ?? { date: '', time: defaultStartTime };
}

export function parseFormDateTime(value: string, timeZoneId: string) {
  const dateTime = DateTime.fromISO(value, { zone: timeZoneId });
  if (!dateTime.isValid) {
    return null;
  }

  return {
    date: dateTime.toFormat('yyyy-MM-dd'),
    time: buildTimeOptionValue(dateTime.hour, dateTime.minute),
  };
}

export function normalizeOptionalText(value?: string | null) {
  const trimmed = value?.trim();
  return trimmed || null;
}

export function normalizeFormTimes<T extends { startTime?: string; endTime?: string }>(formData: T): T {
  return {
    ...formData,
    startTime: normalizeTimeOptionValue(formData.startTime),
    endTime: normalizeTimeOptionValue(formData.endTime),
  };
}

export function normalizeTimeOptionValue(value?: string) {
  if (!value) {
    return value;
  }

  const normalizedValue = normalizeTimeText(value);
  const matchedOption = timeOptions.find((option) => {
    const optionCode = normalizeTimeText(String(option.code));
    const optionLabel = normalizeTimeText(option.description);

    return normalizedValue === optionCode || normalizedValue === optionLabel;
  });

  if (typeof matchedOption?.code === 'string') {
    return matchedOption.code;
  }

  return parseTimeValue(value)?.toFormat('HH:mm') ?? value;
}

export function formatTimeOptionValue(value?: string) {
  if (!value) {
    return '';
  }

  const normalizedValue = normalizeTimeOptionValue(value);
  const option = timeOptions.find((candidate) => candidate.code === normalizedValue);
  if (option) {
    return option.description;
  }

  return parseTimeValue(value)?.toFormat('h:mm a') ?? value;
}

export function formatTimeOptionRange(startTime?: string, endTime?: string) {
  const start = formatTimeOptionValue(startTime);
  const end = formatTimeOptionValue(endTime);

  if (!start && !end) {
    return 'Unknown';
  }

  if (!end) {
    return start;
  }

  if (!start) {
    return end;
  }

  return `${start} - ${end}`;
}

export function normalizeTimeText(value: string) {
  return value.trim().toLowerCase().replace(/\s+/g, '');
}

export function buildTimeOptionValue(hour: number, minute: number) {
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
}

export function buildTimeOptions(): SelectOption[] {
  const options: SelectOption[] = [];

  for (let hour = 0; hour < 24; hour += 1) {
    for (const minute of [0, 30]) {
      const value = buildTimeOptionValue(hour, minute);
      const label = DateTime.fromObject({ hour, minute }).toFormat('h:mm a');
      options.push({ code: value, description: label });
    }
  }

  return options;
}

function parseTimeValue(value: string) {
  return ['H:mm', 'H:mm:ss', 'H:mm:ss.SSS']
    .map((format) => DateTime.fromFormat(value.trim(), format))
    .find((dateTime) => dateTime.isValid);
}
