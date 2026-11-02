import type { LoanOutEntryRequest, LoanOutSeriesRequest } from '@/api-access/generated/models';
import {
  PostApiSchedulingLoanOutEntriesBody,
  PostApiSchedulingLoanOutSeriesBody,
} from '@/api-access/generated/loan-out/loan-out.zod';
import { toUtcCalendarDayRange } from '@/modules/scheduling/schedulingDateTime';
import type { SelectOption } from '@/types/select';
import type { LoanOutFormData } from './components/LoanOutForm.vue';
import { RRule } from 'rrule';
import { DateTime } from 'luxon';
import { mapToValidationErrors } from '@/shared/validation/validationErrors';

export function resolveLoanOutSaveError(data: unknown, error: unknown, fallback = 'Failed to save loan-out.') {
  const mapped = mapToValidationErrors(data);
  if (mapped) {
    return { message: [...new Set(Object.values(mapped))].join(' '), fieldErrors: mapped };
  }
  const message = error && typeof error === 'object' ? (error as { message?: unknown }).message : undefined;
  return { message: typeof message === 'string' && message ? message : fallback, fieldErrors: {} };
}

const constraints = {
  loanOutLocationId:
    PostApiSchedulingLoanOutEntriesBody.shape.loanOutLocationId.positive('Select a loan-out location.'),
  locationId: PostApiSchedulingLoanOutEntriesBody.shape.locationId
    .unwrap()
    .unwrap()
    .positive('Select a location before adding loan-out.'),
  userIds: PostApiSchedulingLoanOutEntriesBody.shape.userIds
    .unwrap()
    .min(1, 'Select at least one employee.')
    .refine((ids) => new Set(ids).size === ids.length, 'Employees must be unique.'),
  startAtUtc: PostApiSchedulingLoanOutEntriesBody.shape.startAtUtc.unwrap(),
  endAtUtc: PostApiSchedulingLoanOutEntriesBody.shape.endAtUtc.unwrap().unwrap(),
  timeZoneId: PostApiSchedulingLoanOutEntriesBody.shape.timeZoneId.unwrap().unwrap().min(1, 'Select a time zone.'),
};

const entrySchema = PostApiSchedulingLoanOutEntriesBody.extend(constraints);
const seriesSchema = PostApiSchedulingLoanOutSeriesBody.extend({
  ...constraints,
  recurrenceRule: PostApiSchedulingLoanOutSeriesBody.shape.recurrenceRule
    .unwrap()
    .unwrap()
    .min(1, 'Set a valid recurrence rule.'),
});

export type LoanOutRequestResult =
  | { kind: 'entry'; request: LoanOutEntryRequest; error?: undefined }
  | { kind: 'series'; request: LoanOutSeriesRequest; error?: undefined }
  | { error: string; fieldErrors?: Record<string, string> };

export function buildLoanOutRequest(
  form: LoanOutFormData,
  options: {
    locationId: number | null | undefined;
    timeZoneId: string;
    loanOutSeriesId?: number | null;
    employeeOptions?: SelectOption[];
  },
): LoanOutRequestResult {
  const userIds = (form.userIds ?? []).filter((id) => typeof id === 'string' && id.length > 0);
  if (!userIds.length)
    return { error: 'Select at least one employee.', fieldErrors: { userIds: 'Select at least one employee.' } };
  const loanOutLocationId = Number(form.loanOutLocationId);
  if (!Number.isInteger(loanOutLocationId) || loanOutLocationId <= 0) {
    return { error: 'Select a loan-out location.', fieldErrors: { loanOutLocationId: 'Select a loan-out location.' } };
  }
  if (options.locationId == null || !Number.isInteger(options.locationId) || options.locationId <= 0) {
    return { error: 'Select a location before adding loan-out.' };
  }
  if (!DateTime.now().setZone(options.timeZoneId).isValid) return { error: 'Select a valid time zone.' };
  const dateRange = toUtcCalendarDayRange(form.date, options.timeZoneId);
  if (!dateRange) {
    return { error: 'Select a valid date.', fieldErrors: { date: 'Select a valid date.' } };
  }
  const commonRequest = {
    loanOutLocationId,
    title: buildLoanOutTitle(
      userIds.map((id) => options.employeeOptions?.find((option) => String(option.code) === id)?.description || id),
    ),
    notes: form.notes?.trim() || null,
    startAtUtc: dateRange.startAtUtc,
    endAtUtc: dateRange.endAtUtc,
    timeZoneId: options.timeZoneId,
    allDay: true,
    locationId: options.locationId,
    userIds,
  };
  if (form.repeatMode === 'custom') {
    const recurrenceError = validateLoanOutRecurrence(form.recurrenceRule, form.date);
    if (recurrenceError) return { error: recurrenceError, fieldErrors: { recurrenceRule: recurrenceError } };
    const result = seriesSchema.safeParse({ ...commonRequest, recurrenceRule: form.recurrenceRule });
    if (result.success) return { kind: 'series', request: result.data };
    return schemaError(result.error.issues);
  }
  const result = entrySchema.safeParse({ ...commonRequest, loanOutSeriesId: options.loanOutSeriesId ?? null });
  if (result.success) return { kind: 'entry', request: result.data };
  return schemaError(result.error.issues);
}

function schemaError(issues: { path: PropertyKey[]; message: string }[]): LoanOutRequestResult {
  const fieldErrors: Record<string, string> = {};
  for (const issue of issues) {
    const field = String(issue.path[0] ?? 'form');
    fieldErrors[field] ??= issue.message;
  }
  return { error: Object.values(fieldErrors).join(' '), fieldErrors };
}

export function validateLoanOutRecurrence(rule: string | null | undefined, date?: string): string {
  if (!rule) return 'Set a valid recurrence rule.';
  try {
    const options = RRule.parseString(rule);
    if (!options.count && !options.until) return 'Recurrence must end after a number of occurrences or on a date.';
    const start = DateTime.fromISO(date ?? '', { zone: 'UTC' }).startOf('day');
    if (!start.isValid) return 'Select a valid start date for the recurrence.';
    const recurrence = new RRule({ ...options, dtstart: start.toJSDate() });
    const occurrences = recurrence.all((_date, index) => index <= 400);
    if (!occurrences.length) return 'The recurrence must include at least one occurrence.';
    if (occurrences.length > 400) return 'Recurrence cannot exceed 400 occurrences.';
    const last = occurrences.at(-1);
    if (last && DateTime.fromJSDate(last, { zone: 'UTC' }).diff(start, 'days').days > 365) {
      return 'Recurrence cannot extend beyond 365 days.';
    }
    return '';
  } catch {
    return 'Set a valid recurrence rule.';
  }
}

export function buildLoanOutTitle(employeeNames: string[]) {
  const names = employeeNames.map((name) => name.trim()).filter(Boolean);
  return names.length ? `${names.join(', ')} loan-out` : 'Loan-out';
}
