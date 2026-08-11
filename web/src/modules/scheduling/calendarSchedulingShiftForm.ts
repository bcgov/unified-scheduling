import type { CalendarEventStatusTypeCode } from '@/api-access/generated/models';
import type { ShiftEntryRequest } from '@/api-access/generated/models/shiftEntryRequest';
import type { ShiftSeriesRequest } from '@/api-access/generated/models/shiftSeriesRequest';
import type { ShiftSeriesResponse } from '@/api-access/generated/models/shiftSeriesResponse';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import { formatUserName } from '@/utils/user';
import type { AssignmentEntryLinkRequest } from '@/api-access/generated/models/assignmentEntryLinkRequest';
import type { AssignmentSeriesLinkRequest } from '@/api-access/generated/models/assignmentSeriesLinkRequest';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixResource } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { SelectOption } from '@/types/select';
import { DateTime } from 'luxon';
import * as zod from 'zod';
import {
  PostApiSchedulingShiftEntriesBody,
  PostApiSchedulingShiftSeriesBody,
} from '@/api-access/generated/shift/shift.zod';
import { validationMessages } from '@/shared/validation/validationErrors';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import {
  dedupeLinksById,
  filterStringArray,
  mapSelectedIdsToAssignedUserLinks,
  parsePositiveInteger,
} from './calendarSchedulingLinkMappers';

export type RepeatMode = 'never' | 'custom';
export type PublishMode = 'yes' | 'no';
export type CancelMode = 'yes' | 'no';

export type ShiftResourceFormData = Partial<zod.infer<typeof PostApiSchedulingShiftEntriesBody>> & {
  date?: string;
  startTime?: string;
  endTime?: string;
  repeatMode: RepeatMode;
  publish: PublishMode;
  cancel: CancelMode;
  recurrenceRule?: string | null;
  assignmentLabel?: string;
  assignmentEntryId?: number | null;
  assignmentEntryIds?: number[];
  assignmentEntryLinks?: AssignmentEntryLinkRequest[];
  assignmentSeriesId?: number | null;
  assignmentSeriesLinks?: AssignmentSeriesLinkRequest[];
  trainingLabel?: string;
};

export type ShiftSavePayload =
  | { kind: 'entry'; body: ShiftEntryRequest; publish: boolean }
  | { kind: 'series'; body: ShiftSeriesRequest; publish: boolean };

export interface ShiftPayloadBuildResult {
  payload: ShiftSavePayload | null;
  errors: Record<string, string>;
}

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
const requiredLinkUserIdsSchema = zod.array(guidLikeSchema).min(1, validationMessages.required);
export const shiftEntryRequestSchema = PostApiSchedulingShiftEntriesBody.extend({
  locationId: zod.number().min(1, validationMessages.required),
  userIds: optionalUserIdsSchema,
  assignmentEntryIds: zod.array(zod.number()).optional(),
  assignedUserIds: optionalUserIdsSchema.nullish(),
  assignmentEntryLinks: zod
    .array(
      zod.object({
        assignmentEntryId: zod.number().optional(),
        assignedUserIds: requiredLinkUserIdsSchema,
      }),
    )
    .nullish(),
});
export const shiftSeriesRequestSchema = PostApiSchedulingShiftSeriesBody.extend({
  locationId: zod.number().min(1, validationMessages.required),
  userIds: optionalUserIdsSchema,
  assignmentSeriesIds: zod.array(zod.number()).optional(),
  assignedUserIds: optionalUserIdsSchema.optional(),
  assignmentSeriesLinks: zod
    .array(
      zod.object({
        assignmentSeriesId: zod.number().optional(),
        assignedUserIds: requiredLinkUserIdsSchema,
      }),
    )
    .nullish(),
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

export const timeOptions = buildTimeOptions();
export const defaultStartTime = buildTimeOptionValue(9, 0);
export const defaultEndTime = buildTimeOptionValue(17, 0);

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
    assignmentEntryId: null,
    assignmentEntryIds: [],
    assignmentEntryLinks: [],
    assignmentSeriesId: null,
    assignmentSeriesLinks: [],
    trainingLabel: '',
    allDay: false,
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
    assignmentEntryId: null,
    assignmentEntryIds: [],
    assignmentEntryLinks: [],
    assignmentSeriesId: null,
    assignmentSeriesLinks: [],
    trainingLabel: '',
    allDay: false,
    statusTypeCode: 'Draft',
    locationId,
    userIds: [],
  };
}

export function createShiftFormDataFromEvent(event: CalendarEventBase, timeZoneId: string): ShiftResourceFormData {
  const start = toFormDateTime(event.start, timeZoneId);
  const end = event.end ? toFormDateTime(event.end, timeZoneId) : null;
  const userIds = resolveEventUserIds(event);

  return {
    title: event.title,
    description: event.description ?? null,
    notes: event.notes ?? '',
    color: event.color ?? null,
    date: start.date,
    startTime: start.time,
    endTime: end?.time ?? defaultEndTime,
    repeatMode: 'never',
    publish: event.statusTypeCode && event.statusTypeCode.toLowerCase() !== 'draft' ? 'yes' : 'no',
    cancel: 'no',
    recurrenceRule: null,
    assignmentLabel: '',
    assignmentEntryId:
      isCalendarSchedulingEvent(event) && event.metadata.assignmentEntryId
        ? Number(event.metadata.assignmentEntryId)
        : null,
    assignmentEntryIds:
      isCalendarSchedulingEvent(event) && event.metadata.assignmentEntryId
        ? [Number(event.metadata.assignmentEntryId)].filter((value) => Number.isInteger(value) && value > 0)
        : [],
    assignmentEntryLinks:
      isCalendarSchedulingEvent(event) && event.metadata.assignmentEntryId
        ? [
            {
              assignmentEntryId: Number(event.metadata.assignmentEntryId),
              assignedUserIds: userIds,
            },
          ].filter((link) => Number.isInteger(link.assignmentEntryId) && Number(link.assignmentEntryId) > 0)
        : [],
    assignmentSeriesId: null,
    assignmentSeriesLinks: [],
    trainingLabel: '',
    allDay: event.allDay ?? false,
    statusTypeCode: event.statusTypeCode ?? 'Draft',
    locationId: event.locationId ?? null,
    userIds,
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
  const assignmentSeriesLinks = resolveAssignmentSeriesLinksFromShiftSeries(series);

  return {
    title: series.title ?? fallbackEvent.title,
    description: series.description ?? null,
    notes: series.notes ?? '',
    color: series.color ?? null,
    date: start.date,
    startTime: start.time,
    endTime: end.time,
    repeatMode: recurrenceRule ? 'custom' : 'never',
    publish: series.statusTypeCode && series.statusTypeCode.toLowerCase() !== 'draft' ? 'yes' : 'no',
    cancel: 'no',
    recurrenceRule,
    assignmentLabel: '',
    assignmentEntryId: null,
    assignmentEntryIds: [],
    assignmentEntryLinks: [],
    assignmentSeriesId: null,
    assignmentSeriesLinks,
    trainingLabel: '',
    allDay: series.allDay ?? false,
    statusTypeCode: series.statusTypeCode ?? 'Draft',
    locationId: series.locationId ?? null,
    userIds: series.userIds ?? [],
  };
}

function resolveAssignmentSeriesLinksFromShiftSeries(series: ShiftSeriesResponse): AssignmentSeriesLinkRequest[] {
  void series;
  return [];
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

export function normalizeShiftFormDataForScope(
  formData: ShiftResourceFormData,
  scope: 'entry' | 'series',
): ShiftResourceFormData {
  const selectedUserIds = formData.userIds?.filter((value): value is string => typeof value === 'string') ?? [];
  const {
    assignmentEntryId: _assignmentEntryId,
    assignmentSeriesId: _assignmentSeriesId,
    ...remainingFormData
  } = formData;
  const withNormalizedLinks = {
    ...remainingFormData,
    assignmentEntryLinks: (formData.assignmentEntryLinks ?? []).map((link) => ({
      ...link,
      assignedUserIds: link.assignedUserIds ?? selectedUserIds,
    })),
    assignmentSeriesLinks: (formData.assignmentSeriesLinks ?? []).map((link) => ({
      ...link,
      assignedUserIds: link.assignedUserIds ?? selectedUserIds,
    })),
  };

  if (scope === 'series') {
    return {
      ...withNormalizedLinks,
      assignmentEntryIds: [],
      assignmentEntryLinks: [],
    };
  }

  return {
    ...withNormalizedLinks,
    assignmentSeriesLinks: [],
  };
}

export function normalizeShiftFormTimes(formData: ShiftResourceFormData): ShiftResourceFormData {
  return {
    ...formData,
    startTime: normalizeTimeOptionValue(formData.startTime),
    endTime: normalizeTimeOptionValue(formData.endTime),
  };
}

export function buildCreateShiftPayload(options: BuildCreateShiftPayloadOptions): ShiftSavePayload | null {
  return buildCreateShiftPayloadWithErrors(options).payload;
}

export function buildCreateShiftPayloadWithErrors(options: BuildCreateShiftPayloadOptions): ShiftPayloadBuildResult {
  return buildShiftPayload({
    ...options,
    scope: options.formData.repeatMode === 'custom' && options.formData.recurrenceRule ? 'series' : 'entry',
    shiftSeriesId: null,
    existingRecurrenceRule: null,
    isCreate: true,
  });
}

export function buildUpdateShiftPayload(options: BuildUpdateShiftPayloadOptions): ShiftSavePayload | null {
  return buildUpdateShiftPayloadWithErrors(options).payload;
}

export function buildUpdateShiftPayloadWithErrors(options: BuildUpdateShiftPayloadOptions): ShiftPayloadBuildResult {
  return buildShiftPayload({
    ...options,
    isCreate: false,
  });
}

function resolveEventUserIds(event: CalendarEventBase) {
  if (!isCalendarSchedulingEvent(event)) {
    return event.resourceIds ?? [];
  }

  if (event.metadata.userIds?.length) {
    return event.metadata.userIds;
  }

  return event.metadata.userId ? [event.metadata.userId] : [];
}

export function buildShiftTitle(employeeName: string) {
  return `${employeeName} shift`;
}

export function buildLocalDateTime(date?: string, time?: string, timeZone?: string) {
  if (!date || !time) {
    return null;
  }

  return DateTime.fromISO(`${date}T${time}`, { zone: timeZone });
}

export function toUtcIso(date?: string, time?: string, timeZone?: string) {
  const dateTime = buildLocalDateTime(date, time, timeZone);
  if (!dateTime?.isValid) {
    return null;
  }

  return dateTime.toUTC().toISO({ suppressMilliseconds: true });
}

export function normalizeOptionalText(value?: string | null) {
  const trimmed = value?.trim();
  return trimmed || null;
}

export function formatUserOptionLabel(user: UserResponse) {
  return formatUserName(user);
}

export function normalizeTimeOptionValue(value?: string) {
  if (!value) {
    return value;
  }

  const normalizedValue = normalizeTimeText(value);
  const parsedTimeOptionValue = parseTimeOptionValue(value);
  if (parsedTimeOptionValue) {
    return parsedTimeOptionValue;
  }

  const matchedOption = timeOptions.find((option) => {
    const optionCode = normalizeTimeText(String(option.code));
    const optionLabel = normalizeTimeText(option.description);

    return normalizedValue === optionCode || normalizedValue === optionLabel;
  });

  return typeof matchedOption?.code === 'string' ? matchedOption.code : value;
}

export function normalizeTimeText(value: string) {
  return value.trim().toLowerCase().replace(/\s+/g, '');
}

function parseTimeOptionValue(value: string) {
  const trimmedValue = value.trim();
  const timeMatch = /^(\d{1,2}):(\d{2})(?::\d{2}(?:\.\d+)?)?$/.exec(trimmedValue);
  if (!timeMatch) {
    return null;
  }

  const hour = Number(timeMatch[1]);
  const minute = Number(timeMatch[2]);
  if (!Number.isInteger(hour) || !Number.isInteger(minute) || hour < 0 || hour > 23 || minute < 0 || minute > 59) {
    return null;
  }

  const optionValue = buildTimeOptionValue(hour, minute);
  return timeOptions.some((option) => option.code === optionValue) ? optionValue : null;
}

export function getFieldErrors(error: zod.ZodError): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    const fullFieldName = issue.path.join('.');
    const message =
      issue.code === 'invalid_type' || issue.code === 'invalid_value' ? validationMessages.required : issue.message;

    if (fullFieldName && !errors[fullFieldName]) {
      errors[fullFieldName] = message;
    }

    if (typeof fieldName === 'string' && !errors[fieldName]) {
      errors[fieldName] = message;
    }
  }
  return errors;
}

export function buildTimeOptionValue(hour: number, minute: number) {
  return `${String(hour).padStart(2, '0')}:${String(minute).padStart(2, '0')}`;
}

export function buildTimeOptions(): SelectOption[] {
  const options: SelectOption[] = [];

  for (let hour = 0; hour < 24; hour += 1) {
    for (const minute of [0, 15, 30, 45]) {
      const value = buildTimeOptionValue(hour, minute);
      const label = DateTime.fromObject({ hour, minute }).toFormat('h:mm a');
      options.push({ code: value, description: label });
    }
  }

  return options;
}

function toFormDateTime(value: string, timeZoneId: string) {
  const dateTime = DateTime.fromISO(value, { zone: timeZoneId });

  if (!dateTime.isValid) {
    return { date: '', time: defaultStartTime };
  }

  return {
    date: dateTime.toFormat('yyyy-MM-dd'),
    time: buildTimeOptionValue(dateTime.hour, dateTime.minute),
  };
}

function createShiftFormSchema(options: ShiftFormValidationOptions) {
  return PostApiSchedulingShiftEntriesBody.partial()
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
      assignmentEntryId: zod.number().nullish(),
      assignmentEntryIds: zod.array(zod.number()).optional(),
      assignmentEntryLinks: zod
        .array(zod.object({ assignmentEntryId: zod.number().optional(), assignedUserIds: requiredLinkUserIdsSchema }))
        .optional(),
      assignmentSeriesId: zod.number().nullish(),
      assignmentSeriesLinks: zod
        .array(zod.object({ assignmentSeriesId: zod.number().optional(), assignedUserIds: requiredLinkUserIdsSchema }))
        .optional(),
      trainingLabel: zod.string().optional(),
      notes: PostApiSchedulingShiftEntriesBody.shape.notes,
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
): ShiftPayloadBuildResult {
  const startAtUtc = toUtcIso(options.formData.date, options.formData.startTime, options.timeZoneId);
  const endAtUtc = toUtcIso(options.formData.date, options.formData.endTime, options.timeZoneId);

  if (!startAtUtc || !endAtUtc || !options.locationId) {
    return {
      payload: null,
      errors: !options.locationId ? { locationId: validationMessages.required } : {},
    };
  }

  const statusTypeCode = String(options.formData.statusTypeCode ?? '').toLowerCase();
  const publish = options.isCreate
    ? options.formData.publish === 'yes'
    : statusTypeCode === 'draft' && options.formData.publish === 'yes';
  const selectedUserIds = options.formData.userIds?.filter((value): value is string => typeof value === 'string') ?? [];
  const assignmentSeriesLinks = resolveAssignmentSeriesLinks(options.formData, selectedUserIds);
  const assignmentEntryLinks = resolveAssignmentEntryLinks(options.formData, selectedUserIds);

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
      assignmentSeriesIds: undefined,
      assignedUserIds: undefined,
      assignmentSeriesLinks,
    };

    const result = shiftSeriesRequestSchema.safeParse(body);
    return result.success
      ? { payload: { kind: 'series', body: result.data, publish }, errors: {} }
      : { payload: null, errors: getShiftPayloadFieldErrors(result.error) };
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
    assignmentEntryIds: undefined,
    assignedUserIds: undefined,
    assignmentEntryLinks,
  };

  const result = shiftEntryRequestSchema.safeParse(body);
  return result.success
    ? { payload: { kind: 'entry', body: result.data, publish }, errors: {} }
    : { payload: null, errors: getShiftPayloadFieldErrors(result.error) };
}

function getShiftPayloadFieldErrors(error: zod.ZodError): Record<string, string> {
  const errors = getFieldErrors(error);

  return {
    ...errors,
    ...(errors.assignmentEntryIds ? { assignmentEntryIds: errors.assignmentEntryIds } : {}),
    ...(errors.assignmentEntryLinks ? { assignmentEntryIds: errors.assignmentEntryLinks } : {}),
    ...(errors.assignmentSeriesIds ? { assignmentSeriesId: errors.assignmentSeriesIds } : {}),
    ...(errors.assignmentSeriesLinks ? { assignmentSeriesId: errors.assignmentSeriesLinks } : {}),
    ...(errors.assignedUserIds ? { userIds: errors.assignedUserIds } : {}),
  };
}

function resolveAssignmentEntryLinks(
  formData: ShiftResourceFormData,
  fallbackUserIds: string[],
): AssignmentEntryLinkRequest[] {
  const links = (formData.assignmentEntryLinks ?? [])
    .map((link) => ({
      assignmentEntryId: parsePositiveInteger(link.assignmentEntryId)[0],
      assignedUserIds: filterStringArray(link.assignedUserIds ?? fallbackUserIds),
    }))
    .filter((link): link is { assignmentEntryId: number; assignedUserIds: string[] } =>
      Number.isInteger(link.assignmentEntryId),
    );

  if (links.length) {
    return dedupeLinksById(links, 'assignmentEntryId');
  }

  const assignmentEntryLinks = mapSelectedIdsToAssignedUserLinks(
    formData.assignmentEntryIds,
    'assignmentEntryId',
    fallbackUserIds,
  );
  if (assignmentEntryLinks.length) {
    return assignmentEntryLinks;
  }

  return parsePositiveInteger(formData.assignmentEntryId).map((assignmentEntryId) => ({
    assignmentEntryId,
    assignedUserIds: fallbackUserIds,
  }));
}

function resolveAssignmentSeriesLinks(
  formData: ShiftResourceFormData,
  fallbackUserIds: string[],
): AssignmentSeriesLinkRequest[] {
  const links = (formData.assignmentSeriesLinks ?? [])
    .map((link) => ({
      assignmentSeriesId: parsePositiveInteger(link.assignmentSeriesId)[0],
      assignedUserIds: filterStringArray(link.assignedUserIds ?? fallbackUserIds),
    }))
    .filter((link): link is { assignmentSeriesId: number; assignedUserIds: string[] } =>
      Number.isInteger(link.assignmentSeriesId),
    );

  if (links.length) {
    return dedupeLinksById(links, 'assignmentSeriesId');
  }

  return parsePositiveInteger(formData.assignmentSeriesId).map((assignmentSeriesId) => ({
    assignmentSeriesId,
    assignedUserIds: fallbackUserIds,
  }));
}
