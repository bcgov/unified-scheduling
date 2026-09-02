import type { AssignmentEntryRequest } from '@/api-access/generated/models/assignmentEntryRequest';
import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { AssignmentSeriesRequest } from '@/api-access/generated/models/assignmentSeriesRequest';
import {
  PostApiSchedulingAssignmentsEntriesBody,
  PostApiSchedulingAssignmentsSeriesBody,
} from '@/api-access/generated/assignment/assignment.zod';
import { validationMessages } from '@/shared/validation/validationErrors';
import type { SelectOption } from '@/types/select';
import * as zod from 'zod';
import { getFieldErrors, type RepeatMode } from './calendarSchedulingShiftForm';
import { mapLoadedAssignedUserLinks } from './calendarSchedulingLinkMappers';
import {
  buildLocalDateTime,
  defaultEndTime,
  defaultStartTime,
  normalizeFormTimes,
  normalizeOptionalText,
  toUtcIso,
} from './schedulingDateTime';

type AssignmentRequestFormFields = Omit<
  Partial<zod.infer<typeof PostApiSchedulingAssignmentsEntriesBody>>,
  'color' | 'locationId'
>;

export type AssignmentFormData = AssignmentRequestFormFields & {
  color?: string | null;
  locationId?: number | null;
  assignmentDefinitionId?: number;
  categoryId?: number;
  subCategoryId?: number;
  capacity?: number;
  date?: string;
  startTime?: string;
  endTime?: string;
  repeatMode: RepeatMode;
  recurrenceRule?: string | null;
  assignmentSeriesId?: number | null;
  seriesStartAtUtc?: string | null;
  seriesEndAtUtc?: string | null;
  shiftSeriesIds?: number[];
  shiftEntryIds?: number[];
  shiftSeriesLinks?: ShiftSeriesLinkFormData[];
  shiftEntryLinks?: ShiftEntryLinkFormData[];
};

export interface ShiftSeriesLinkFormData {
  id?: number;
  shiftSeriesId: number;
  assignedUserIds: string[];
}

export interface ShiftEntryLinkFormData {
  id?: number;
  shiftEntryId: number;
  assignedUserIds: string[];
}

export type AssignmentSavePayload =
  | { kind: 'entry'; body: AssignmentEntryRequest; links: ShiftEntryLinkFormData[] }
  | { kind: 'series'; body: AssignmentSeriesRequest; links: ShiftSeriesLinkFormData[] };

export interface AssignmentFormValidationOptions {
  timeZoneId: string;
  recurrenceError?: string;
}

export interface BuildCreateAssignmentPayloadOptions {
  formData: AssignmentFormData;
  timeZoneId: string;
  locationId: number | null;
  assignmentOptions: SelectOption[];
}

const defaultCapacity = 1;
export const defaultAssignmentColor = 'white';

const assignmentEntryRequestSchema = PostApiSchedulingAssignmentsEntriesBody.extend({
  locationId: zod.number().min(1, validationMessages.required),
  assignmentDefinitionId: zod.number().min(1),
  categoryId: zod.number().min(1),
  subCategoryId: zod.number().min(1),
  capacity: zod.number().min(1),
  shiftEntryIds: zod.array(zod.number()).optional(),
  shiftEntryLinks: zod
    .array(
      zod.object({
        id: zod.number().int().positive().optional(),
        shiftEntryId: zod.number().min(1),
        assignedUserIds: zod.array(zod.string().uuid()).min(1),
      }),
    )
    .optional(),
});

const assignmentSeriesRequestSchema = PostApiSchedulingAssignmentsSeriesBody.extend({
  locationId: zod.number().min(1, validationMessages.required),
  assignmentDefinitionId: zod.number().min(1),
  categoryId: zod.number().min(1),
  subCategoryId: zod.number().min(1),
  capacity: zod.number().min(1),
  recurrenceRule: zod.string().min(1, validationMessages.required),
  shiftSeriesIds: zod.array(zod.number()).optional(),
  shiftSeriesLinks: zod
    .array(
      zod.object({
        id: zod.number().int().positive().optional(),
        shiftSeriesId: zod.number().min(1),
        assignedUserIds: zod.array(zod.string().uuid()).min(1),
      }),
    )
    .optional(),
});

export function createInitialAssignmentFormData(initialDate?: string): AssignmentFormData {
  return {
    title: '',
    description: null,
    notes: '',
    color: defaultAssignmentColor,
    date: initialDate ?? '',
    startTime: defaultStartTime,
    endTime: defaultEndTime,
    repeatMode: 'never',
    recurrenceRule: null,
    assignmentSeriesId: null,
    seriesStartAtUtc: null,
    seriesEndAtUtc: null,
    allDay: false,
    locationId: null,
    assignmentDefinitionId: undefined,
    categoryId: undefined,
    subCategoryId: undefined,
    capacity: defaultCapacity,
    shiftSeriesIds: [],
    shiftEntryIds: [],
    shiftSeriesLinks: [],
    shiftEntryLinks: [],
  };
}

export function resolveShiftEntryLinksFromAssignmentEntry(entry: AssignmentEntryResponse): ShiftEntryLinkFormData[] {
  const assignmentLinks = entry.assignmentLinks ?? [];

  if (assignmentLinks.length) {
    return mapLoadedAssignedUserLinks(assignmentLinks, 'shiftEntryId', (link) => link.shiftEntryId);
  }

  const assignedUserIds = entry.assignedUserIds ?? [];
  return (entry.linkedShiftEntryIds ?? []).map((shiftEntryId) => ({
    shiftEntryId,
    assignedUserIds,
  }));
}

export function normalizeAssignmentFormTimes(formData: AssignmentFormData): AssignmentFormData {
  return normalizeFormTimes(formData);
}

export function validateAssignmentFormData(
  formData: AssignmentFormData,
  options: AssignmentFormValidationOptions,
): { data: AssignmentFormData; errors: Record<string, string> } | { data: null; errors: Record<string, string> } {
  const normalizedFormData = normalizeAssignmentFormTimes(formData);
  const result = createAssignmentFormSchema(options).safeParse(normalizedFormData);

  if (!result.success) {
    return {
      data: null,
      errors: getFieldErrors(result.error),
    };
  }

  return {
    data: result.data,
    errors: {},
  };
}

export function buildCreateAssignmentPayload(
  options: BuildCreateAssignmentPayloadOptions,
): AssignmentSavePayload | null {
  const startAtUtc = toUtcIso(options.formData.date, options.formData.startTime, options.timeZoneId);
  const endAtUtc = toUtcIso(options.formData.date, options.formData.endTime, options.timeZoneId);
  const assignmentDefinitionId = options.formData.assignmentDefinitionId;
  const categoryId = options.formData.categoryId;
  const subCategoryId = options.formData.subCategoryId;

  if (!startAtUtc || !endAtUtc || !options.locationId || !assignmentDefinitionId || !categoryId || !subCategoryId) {
    return null;
  }

  const title =
    options.formData.title?.trim() || resolveAssignmentTitle(assignmentDefinitionId, options.assignmentOptions);
  const common = {
    title,
    description: normalizeOptionalText(options.formData.description),
    notes: normalizeOptionalText(options.formData.notes),
    color: options.formData.color?.trim() || defaultAssignmentColor,
    startAtUtc,
    endAtUtc,
    timeZoneId: options.timeZoneId,
    allDay: false,
    locationId: options.locationId,
    assignmentDefinitionId,
    categoryId,
    subCategoryId,
    capacity: options.formData.capacity ?? defaultCapacity,
  };

  if (options.formData.repeatMode === 'custom' && options.formData.recurrenceRule) {
    const result = assignmentSeriesRequestSchema.safeParse({
      ...common,
      recurrenceRule: options.formData.recurrenceRule,
      shiftSeriesLinks: options.formData.shiftSeriesLinks ?? [],
    });
    if (!result.success) {
      return null;
    }

    const { shiftSeriesIds: _shiftSeriesIds, ...body } = result.data;
    const shiftSeriesLinks = result.data.shiftSeriesLinks ?? [];
    return { kind: 'series', body, links: shiftSeriesLinks ?? [] };
  }

  const result = assignmentEntryRequestSchema.safeParse({
    ...common,
    assignmentSeriesId: options.formData.assignmentSeriesId ?? null,
    shiftEntryLinks: options.formData.shiftEntryLinks ?? [],
    seriesStartAtUtc: options.formData.seriesStartAtUtc ?? null,
    seriesEndAtUtc: options.formData.seriesEndAtUtc ?? null,
  });
  if (!result.success) {
    return null;
  }

  const { shiftEntryIds: _shiftEntryIds, shiftEntryLinks, ...body } = result.data;
  return { kind: 'entry', body, links: shiftEntryLinks ?? [] };
}

function createAssignmentFormSchema(options: AssignmentFormValidationOptions) {
  return PostApiSchedulingAssignmentsEntriesBody.partial()
    .extend({
      assignmentDefinitionId: zod.number().min(1, validationMessages.required),
      date: zod.string().min(1, validationMessages.required),
      startTime: zod.string().min(1, validationMessages.required),
      endTime: zod.string().min(1, validationMessages.required),
      repeatMode: zod.enum(['never', 'custom']),
      recurrenceRule: zod.string().nullish(),
      notes: PostApiSchedulingAssignmentsEntriesBody.shape.notes,
      categoryId: zod.number().min(1, validationMessages.required),
      subCategoryId: zod.number().min(1, validationMessages.required),
      capacity: zod.number().min(1),
      shiftSeriesIds: zod.array(zod.number()).optional(),
      shiftEntryIds: zod.array(zod.number()).optional(),
      shiftSeriesLinks: zod
        .array(
          zod.object({
            id: zod.number().int().positive().optional(),
            shiftSeriesId: zod.number().min(1),
            assignedUserIds: zod.array(zod.string().uuid()).min(1),
          }),
        )
        .optional(),
      shiftEntryLinks: zod
        .array(
          zod.object({
            id: zod.number().int().positive().optional(),
            shiftEntryId: zod.number().min(1),
            assignedUserIds: zod.array(zod.string().uuid()).min(1),
          }),
        )
        .optional(),
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

function resolveAssignmentTitle(assignmentDefinitionId: number, assignmentOptions: SelectOption[]) {
  const option = assignmentOptions.find((candidate) => Number(candidate.code) === assignmentDefinitionId);
  return option?.description || 'Assignment';
}
