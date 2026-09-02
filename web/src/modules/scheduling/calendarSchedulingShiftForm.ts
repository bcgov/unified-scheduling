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
import { getFieldErrors, validationMessages } from '@/shared/validation/validationErrors';
import { resolveCalendarEventUserIds } from './calendarSchedulingEventUsers';
import { filterStringArray } from './calendarSchedulingLinkMappers';
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

export interface ShiftAssignmentEntryLinkFormData {
  assignmentEntryId: number;
  assignedUserIds: string[];
}

export interface ShiftAssignmentSeriesLinkFormData {
  assignmentSeriesId: number;
  assignedUserIds: string[];
}

export type ShiftResourceFormData = Omit<
  Partial<zod.infer<typeof PostApiSchedulingShiftsEntriesBody>>,
  'assignmentEntryLinks' | 'assignmentSeriesLinks'
> & {
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
  assignmentEntryLinks: zod
    .array(
      zod.object({
        assignmentEntryId: zod.number().int().positive(),
        assignedUserIds: zod.array(guidLikeSchema).min(1),
      }),
    )
    .optional(),
});
export const shiftSeriesRequestSchema = PostApiSchedulingShiftsSeriesBody.extend({
  userIds: optionalUserIdsSchema,
  assignmentSeriesLinks: zod
    .array(
      zod.object({
        assignmentSeriesId: zod.number().int().positive(),
        assignedUserIds: zod.array(guidLikeSchema).min(1),
      }),
    )
    .optional(),
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

  return {
    ...formData,
    userIds: selectedUserIds,
    assignmentEntryLinks: scope === 'entry' ? (formData.assignmentEntryLinks ?? []) : [],
    assignmentSeriesLinks: scope === 'series' ? (formData.assignmentSeriesLinks ?? []) : [],
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
      assignmentEntryLinks: zod
        .array(
          zod.object({
            assignmentEntryId: zod.number().int().positive(),
            assignedUserIds: zod.array(guidLikeSchema).min(1),
          }),
        )
        .optional(),
      assignmentSeriesLinks: zod
        .array(
          zod.object({
            assignmentSeriesId: zod.number().int().positive(),
            assignedUserIds: zod.array(guidLikeSchema).min(1),
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
  const formData = normalizeShiftFormDataForScope(options.formData, options.scope);
  const startAtUtc = toUtcIso(formData.date, formData.startTime, options.timeZoneId);
  const endAtUtc = toUtcIso(formData.date, formData.endTime, options.timeZoneId);

  if (!startAtUtc || !endAtUtc) {
    return null;
  }

  const lifecycleStatus = normalizeSchedulingLifecycleStatus(formData.statusTypeCode);
  const publish = options.isCreate
    ? formData.publish === 'yes'
    : lifecycleStatus === 'draft' && formData.publish === 'yes';
  const cancel = !options.isCreate && lifecycleStatus === 'published' && formData.cancel === 'yes';
  const selectedUserIds = formData.userIds?.filter((value): value is string => typeof value === 'string') ?? [];

  if (options.scope === 'series') {
    const body: ShiftSeriesRequest = {
      title: formData.title ?? buildShiftTitle(options.fallbackTitle),
      description: formData.description ?? null,
      notes: normalizeOptionalText(formData.notes),
      color: null,
      recurrenceRule: formData.recurrenceRule ?? options.existingRecurrenceRule ?? null,
      timeZoneId: options.timeZoneId,
      startAtUtc,
      endAtUtc,
      allDay: false,
      locationId: options.locationId,
      userIds: selectedUserIds,
      assignmentSeriesLinks: formData.assignmentSeriesLinks ?? [],
    };

    const result = shiftSeriesRequestSchema.safeParse(body);
    return result.success ? { kind: 'series', body: result.data, publish, cancel } : null;
  }

  const body: ShiftEntryRequest = {
    shiftSeriesId: options.shiftSeriesId,
    title: formData.title ?? buildShiftTitle(options.fallbackTitle),
    description: formData.description ?? null,
    notes: normalizeOptionalText(formData.notes),
    color: null,
    startAtUtc,
    endAtUtc,
    seriesStartAtUtc: null,
    seriesEndAtUtc: null,
    timeZoneId: options.timeZoneId,
    allDay: false,
    locationId: options.locationId,
    userIds: selectedUserIds,
    assignmentEntryLinks: formData.assignmentEntryLinks ?? [],
  };

  const result = shiftEntryRequestSchema.safeParse(body);
  return result.success ? { kind: 'entry', body: result.data, publish, cancel } : null;
}

export function buildShiftTitle(employeeName: string) {
  return `${employeeName} shift`;
}
export function formatUserOptionLabel(user: UserResponse) {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName || user.idirName;
}
