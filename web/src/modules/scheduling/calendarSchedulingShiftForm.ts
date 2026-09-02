import type { CalendarEventStatusTypeCode } from '@/api-access/generated/models';
import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftEntryResponse } from '@/api-access/generated/models/shiftEntryResponse';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { SelectOption } from '@/types/select';
import * as zod from 'zod';
import {
  PostApiSchedulingShiftsEntriesBody,
  PostApiSchedulingShiftsSeriesBody,
} from '@/api-access/generated/shift/shift.zod';
import { validationMessages } from '@/shared/validation/validationErrors';
import { resolveCalendarEventUserIds } from './calendarSchedulingEventUsers';
import { filterStringArray } from './calendarSchedulingLinkMappers';
import { parsePositiveInteger } from './calendarSchedulingShiftIds';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';
import {
  buildLocalDateTime,
  defaultEndTime,
  defaultStartTime,
  normalizeFormTimes,
  normalizeOptionalText,
  toFormDateTime,
  toUtcIso,
} from './schedulingDateTime';

export type RepeatMode = 'never' | 'custom';
export type PublishMode = 'yes' | 'no';
export type CancelMode = 'yes' | 'no';

export interface ShiftAssignmentLinkFormData {
  assignmentEntryId?: number;
  assignmentSeriesId?: number | null;
  assignedUserIds?: string[];
  userIds?: string[];
}

export type ShiftAssignmentEntryLinkFormData = ShiftAssignmentLinkFormData;
export type ShiftAssignmentSeriesLinkFormData = ShiftAssignmentLinkFormData;

export type ShiftResourceFormData = Partial<zod.infer<typeof PostApiSchedulingShiftsEntriesBody>> & {
  date?: string;
  startTime?: string;
  endTime?: string;
  repeatMode: RepeatMode;
  publish: PublishMode;
  cancel: CancelMode;
  recurrenceRule?: string | null;
  assignmentLabel?: string;
  trainingLabel?: string;
  isException?: boolean;
  statusTypeCode?: string;
  assignmentEntryIds?: unknown[];
  assignmentEntryId?: number | null;
  assignmentSeriesId?: number | null;
  assignmentEntryLinks?: ShiftAssignmentEntryLinkFormData[];
  assignmentSeriesLinks?: ShiftAssignmentSeriesLinkFormData[];
};

export type ShiftSavePayload =
  | { kind: 'entry'; body: ShiftEntryRequest; publish: boolean; cancel: boolean }
  | { kind: 'series'; body: ShiftSeriesRequest; publish: boolean; cancel: boolean };

export interface ShiftFormValidationOptions {
  timeZoneId: string;
  recurrenceError?: string;
  requireCancel?: boolean;
}

export interface BuildCreateShiftPayloadOptions {
  formData: ShiftResourceFormData;
  timeZoneId: string;
  locationId: number | null;
  fallbackTitle: string;
}

export interface BuildUpdateShiftPayloadOptions {
  formData: ShiftResourceFormData;
  scope: 'entry' | 'series';
  timeZoneId: string;
  locationId: number | null;
  fallbackTitle: string;
  shiftSeriesId: number | null;
  existingRecurrenceRule?: string | null;
}

const guidLikeSchema = zod
  .string()
  .regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/, {
    message: 'Invalid UUID',
  });
const optionalUserIdsSchema = zod.array(guidLikeSchema).optional();
export const shiftEntryRequestSchema = PostApiSchedulingShiftsEntriesBody.extend({
  userIds: optionalUserIdsSchema,
});
export const shiftSeriesRequestSchema = PostApiSchedulingShiftsSeriesBody.extend({
  userIds: optionalUserIdsSchema,
});

export const repeatOptions: SelectOption[] = [
  { code: 'never', description: 'Never' },
  { code: 'custom', description: 'Custom recurrence' },
];

export const publishOptions: SelectOption[] = [
  { code: 'no', description: 'No' },
  { code: 'yes', description: 'Yes' },
];
export const cancelOptions: SelectOption[] = [
  { code: 'no', description: 'No' },
  { code: 'yes', description: 'Yes' },
];

export function createInitialShiftFormData(
  resource: CalendarMatrixResource,
  locationId: number | null,
  statusTypeCode: CalendarEventStatusTypeCode | string,
): ShiftResourceFormData {
  return {
    title: buildShiftTitle(resource.title || resource.id),
    description: null,
    notes: '',
    color: null,
    date: '',
    startTime: defaultStartTime,
    endTime: defaultEndTime,
    repeatMode: 'never',
    publish: 'no',
    cancel: 'no',
    recurrenceRule: null,
    assignmentLabel: '',
    trainingLabel: '',
    allDay: false,
    isException: false,
    statusTypeCode,
    locationId,
    userIds: resource.type === 'user' ? [resource.id] : [],
  };
}

export function createInitialShiftFormDataForCreateAction(locationId: number | null): ShiftResourceFormData {
  return {
    title: 'New shift',
    description: null,
    notes: '',
    color: null,
    date: '',
    startTime: defaultStartTime,
    endTime: defaultEndTime,
    repeatMode: 'never',
    publish: 'no',
    cancel: 'no',
    recurrenceRule: null,
    assignmentLabel: '',
    trainingLabel: '',
    allDay: false,
    isException: false,
    statusTypeCode: 'Draft',
    locationId,
    userIds: [],
  };
}

export function createShiftFormDataFromEvent(event: CalendarEventBase, timeZoneId: string): ShiftResourceFormData {
  const start = toFormDateTime(event.start, timeZoneId);
  const end = event.end ? toFormDateTime(event.end, timeZoneId) : null;
  const userIds = resolveCalendarEventUserIds(event, { fallbackToResourceIds: false });

  return {
    title: event.title,
    description: event.description ?? null,
    notes: event.notes ?? '',
    color: event.color ?? null,
    date: start.date,
    startTime: start.time,
    endTime: end?.time ?? defaultEndTime,
    repeatMode: 'never',
    publish: normalizeSchedulingLifecycleStatus(event.statusTypeCode) === 'published' ? 'yes' : 'no',
    cancel: 'no',
    recurrenceRule: null,
    assignmentLabel: '',
    trainingLabel: '',
    allDay: event.allDay ?? false,
    isException: event.isException ?? false,
    statusTypeCode: event.statusTypeCode ?? 'Draft',
    locationId: event.locationId ?? null,
    userIds,
  };
}

export function createShiftFormDataFromEntry(
  entry: ShiftEntryResponse,
  fallbackEvent: CalendarEventBase,
  timeZoneId: string,
): ShiftResourceFormData {
  const assignmentEntryLinks = (entry.assignmentLinks ?? []).flatMap((link) =>
    typeof link.assignmentEntryId === 'number'
      ? [{ assignmentEntryId: link.assignmentEntryId, assignedUserIds: link.userIds ?? [] }]
      : [],
  );
  const event = {
    ...fallbackEvent,
    title: entry.title ?? fallbackEvent.title,
    start: entry.startAtUtc ?? fallbackEvent.start,
    end: entry.endAtUtc ?? fallbackEvent.end,
    timeZoneId: entry.timeZoneId ?? fallbackEvent.timeZoneId,
    statusTypeCode: entry.statusTypeCode ?? fallbackEvent.statusTypeCode,
    locationId: entry.locationId ?? fallbackEvent.locationId,
    resourceIds: entry.userIds ?? fallbackEvent.resourceIds,
  };

  return {
    ...createShiftFormDataFromEvent(event, timeZoneId),
    assignmentEntryId: assignmentEntryLinks.length === 1 ? assignmentEntryLinks[0]?.assignmentEntryId : null,
    assignmentEntryIds: assignmentEntryLinks.map((link) => link.assignmentEntryId),
    assignmentEntryLinks,
  };
}

export function createShiftFormDataFromSeries(
  series: ShiftSeriesResponse,
  fallbackEvent: CalendarEventBase,
  timeZoneId: string,
): ShiftResourceFormData {
  const start = toFormDateTime(series.startAtUtc ?? fallbackEvent.start, timeZoneId);
  const end = toFormDateTime(series.endAtUtc ?? fallbackEvent.end ?? fallbackEvent.start, timeZoneId);
  const recurrenceRule = series.recurrenceRule ?? null;

  return {
    title: series.title ?? fallbackEvent.title,
    description: series.description ?? null,
    notes: series.notes ?? '',
    color: series.color ?? null,
    date: start.date,
    startTime: start.time,
    endTime: end.time,
    repeatMode: recurrenceRule ? 'custom' : 'never',
    publish: normalizeSchedulingLifecycleStatus(series.statusTypeCode) === 'published' ? 'yes' : 'no',
    cancel: 'no',
    recurrenceRule,
    assignmentLabel: '',
    trainingLabel: '',
    allDay: series.allDay ?? false,
    isException: false,
    statusTypeCode: series.statusTypeCode ?? 'Draft',
    locationId: series.locationId ?? null,
    userIds: series.userIds ?? [],
  };
}

export function validateShiftFormData(
  formData: ShiftResourceFormData,
  options: ShiftFormValidationOptions,
): { data: ShiftResourceFormData; errors: Record<string, string> } | { data: null; errors: Record<string, string> } {
  const normalizedFormData = normalizeShiftFormTimes(formData);
  const schema = createShiftFormSchema(options);
  const result = schema.safeParse(normalizedFormData);

  if (!result.success) {
    return {
      data: null,
      errors: getFieldErrors(result.error),
    };
  }

  return {
    data: {
      ...result.data,
      cancel: result.data.cancel ?? 'no',
    },
    errors: {},
  };
}

export function normalizeShiftFormTimes(formData: ShiftResourceFormData): ShiftResourceFormData {
  return normalizeFormTimes(formData);
}

export function normalizeShiftFormDataForScope(
  formData: ShiftResourceFormData,
  scope: 'entry' | 'series',
): ShiftResourceFormData {
  const selectedUserIds = filterStringArray(formData.userIds);
  const assignmentEntryLinks = normalizeAssignmentLinks(
    formData.assignmentEntryLinks,
    formData.assignmentEntryIds,
    'assignmentEntryId',
    selectedUserIds,
  );
  const assignmentSeriesLinks = normalizeAssignmentLinks(
    formData.assignmentSeriesLinks,
    formData.assignmentSeriesId == null ? [] : [formData.assignmentSeriesId],
    'assignmentSeriesId',
    selectedUserIds,
  );
  const {
    assignmentEntryIds: _assignmentEntryIds,
    assignmentEntryId: _assignmentEntryId,
    assignmentSeriesId: _assignmentSeriesId,
    ...normalized
  } = formData;

  return {
    ...normalized,
    userIds: selectedUserIds,
    assignmentEntryLinks: scope === 'entry' ? assignmentEntryLinks : [],
    assignmentSeriesLinks: scope === 'series' ? assignmentSeriesLinks : [],
  };
}

export function buildCreateShiftPayload(options: BuildCreateShiftPayloadOptions): ShiftSavePayload | null {
  return buildShiftPayload({
    ...options,
    scope: options.formData.repeatMode === 'custom' && options.formData.recurrenceRule ? 'series' : 'entry',
    shiftSeriesId: null,
    existingRecurrenceRule: null,
    isCreate: true,
  });
}

export function buildCreateShiftPayloadWithErrors(options: BuildCreateShiftPayloadOptions): {
  payload: ShiftSavePayload | null;
  errors: Record<string, string>;
} {
  if (options.locationId == null) {
    return { payload: null, errors: { locationId: validationMessages.required } };
  }

  return { payload: buildCreateShiftPayload(options), errors: {} };
}

export function buildUpdateShiftPayload(options: BuildUpdateShiftPayloadOptions): ShiftSavePayload | null {
  return buildShiftPayload({
    ...options,
    isCreate: false,
  });
}

export function buildUpdateShiftPayloadWithErrors(options: BuildUpdateShiftPayloadOptions): {
  payload: ShiftSavePayload | null;
  errors: Record<string, string>;
} {
  if (options.locationId == null) {
    return { payload: null, errors: { locationId: validationMessages.required } };
  }

  return { payload: buildUpdateShiftPayload(options), errors: {} };
}

export function buildShiftTitle(employeeName: string) {
  return `${employeeName} shift`;
}

export function formatUserOptionLabel(user: UserResponse) {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName || user.idirName;
}

export function getFieldErrors(error: zod.ZodError): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      if (issue.code === 'invalid_type' || issue.code === 'invalid_value') {
        errors[fieldName] = validationMessages.required;
        continue;
      }

      errors[fieldName] = issue.message;
    }
  }
  return errors;
}

function createShiftFormSchema(options: ShiftFormValidationOptions) {
  return PostApiSchedulingShiftsEntriesBody.partial()
    .extend({
      date: zod.string().min(1, validationMessages.required),
      startTime: zod.string().min(1, validationMessages.required),
      endTime: zod.string().min(1, validationMessages.required),
      repeatMode: zod.enum(['never', 'custom']),
      publish: zod.enum(['yes', 'no']),
      cancel: options.requireCancel ? zod.enum(['yes', 'no']) : zod.enum(['yes', 'no']).optional(),
      userIds: optionalUserIdsSchema,
      recurrenceRule: zod.string().nullish(),
      assignmentLabel: zod.string().optional(),
      trainingLabel: zod.string().optional(),
      isException: zod.boolean().optional(),
      statusTypeCode: zod.string().optional(),
      assignmentEntryIds: zod.array(zod.number().int().positive()).optional(),
      assignmentEntryId: zod.number().int().positive().nullish(),
      assignmentSeriesId: zod.number().int().positive().nullish(),
      assignmentEntryLinks: zod
        .array(
          zod.object({
            assignmentEntryId: zod.number().int().positive(),
            assignedUserIds: zod.array(zod.string().uuid()).min(1),
          }),
        )
        .optional(),
      assignmentSeriesLinks: zod
        .array(
          zod.object({
            assignmentSeriesId: zod.number().int().positive(),
            assignedUserIds: zod.array(zod.string().uuid()).min(1),
          }),
        )
        .optional(),
      notes: PostApiSchedulingShiftsEntriesBody.shape.notes,
    })
    .superRefine((data, ctx) => {
      const startDateTime = buildLocalDateTime(data.date, data.startTime, options.timeZoneId);
      const endDateTime = buildLocalDateTime(data.date, data.endTime, options.timeZoneId);

      if (!startDateTime?.isValid) {
        ctx.addIssue({ code: 'custom', path: ['startTime'], message: 'Invalid start time.' });
      }

      if (!endDateTime?.isValid) {
        ctx.addIssue({ code: 'custom', path: ['endTime'], message: 'Invalid end time.' });
      }

      if (startDateTime?.isValid && endDateTime?.isValid && endDateTime <= startDateTime) {
        ctx.addIssue({ code: 'custom', path: ['endTime'], message: 'End time must be after start time.' });
      }

      if (data.repeatMode === 'custom' && !data.recurrenceRule) {
        ctx.addIssue({ code: 'custom', path: ['recurrenceRule'], message: validationMessages.required });
      }

      if (data.repeatMode === 'custom' && options.recurrenceError) {
        ctx.addIssue({ code: 'custom', path: ['recurrenceRule'], message: options.recurrenceError });
      }
    });
}

function buildShiftPayload(
  options: (BuildCreateShiftPayloadOptions | BuildUpdateShiftPayloadOptions) & {
    scope: 'entry' | 'series';
    shiftSeriesId: number | null;
    existingRecurrenceRule?: string | null;
    isCreate: boolean;
  },
): ShiftSavePayload | null {
  const startAtUtc = toUtcIso(options.formData.date, options.formData.startTime, options.timeZoneId);
  const endAtUtc = toUtcIso(options.formData.date, options.formData.endTime, options.timeZoneId);

  if (!startAtUtc || !endAtUtc) {
    return null;
  }

  const lifecycleStatus = normalizeSchedulingLifecycleStatus(options.formData.statusTypeCode);
  const publish = options.isCreate
    ? options.formData.publish === 'yes'
    : lifecycleStatus === 'draft' && options.formData.publish === 'yes';
  const cancel = !options.isCreate && lifecycleStatus === 'published' && options.formData.cancel === 'yes';
  const selectedUserIds = options.formData.userIds?.filter((value): value is string => typeof value === 'string') ?? [];

  if (options.scope === 'series') {
    const body: ShiftSeriesRequest = {
      title: options.formData.title ?? buildShiftTitle(options.fallbackTitle),
      description: options.formData.description ?? null,
      notes: normalizeOptionalText(options.formData.notes),
      color: null,
      recurrenceRule: options.formData.recurrenceRule ?? options.existingRecurrenceRule ?? null,
      timeZoneId: options.timeZoneId,
      startAtUtc,
      endAtUtc,
      allDay: false,
      locationId: options.locationId,
      userIds: selectedUserIds,
    };

    const result = shiftSeriesRequestSchema.safeParse(body);
    return result.success ? { kind: 'series', body: result.data, publish, cancel } : null;
  }

  const body: ShiftEntryRequest = {
    shiftSeriesId: options.shiftSeriesId,
    title: options.formData.title ?? buildShiftTitle(options.fallbackTitle),
    description: options.formData.description ?? null,
    notes: normalizeOptionalText(options.formData.notes),
    color: null,
    startAtUtc,
    endAtUtc,
    seriesStartAtUtc: null,
    seriesEndAtUtc: null,
    timeZoneId: options.timeZoneId,
    allDay: false,
    locationId: options.locationId,
    userIds: selectedUserIds,
  };

  const result = shiftEntryRequestSchema.safeParse(body);
  return result.success ? { kind: 'entry', body: result.data, publish, cancel } : null;
}

function normalizeAssignmentLinks(
  links: ShiftAssignmentLinkFormData[] | undefined,
  selectedIds: unknown[] | undefined,
  idKey: 'assignmentEntryId' | 'assignmentSeriesId',
  defaultUserIds: string[],
) {
  const candidates: ShiftAssignmentLinkFormData[] = links?.length
    ? links
    : (selectedIds ?? []).map((id) => ({ [idKey]: id }));
  const normalizedById = new Map<number, Record<string, number | string[]>>();

  for (const link of candidates) {
    const id = parsePositiveInteger(link[idKey]);
    if (!id) {
      continue;
    }

    const assignedUserIds = filterStringArray(link.assignedUserIds ?? link.userIds);
    normalizedById.set(id, {
      [idKey]: id,
      assignedUserIds: assignedUserIds.length ? assignedUserIds : defaultUserIds,
    });
  }

  return Array.from(normalizedById.values()).map((link) => ({
    assignmentEntryId: typeof link.assignmentEntryId === 'number' ? link.assignmentEntryId : undefined,
    assignmentSeriesId: typeof link.assignmentSeriesId === 'number' ? link.assignmentSeriesId : undefined,
    assignedUserIds: Array.isArray(link.assignedUserIds) ? link.assignedUserIds : [],
  }));
}
