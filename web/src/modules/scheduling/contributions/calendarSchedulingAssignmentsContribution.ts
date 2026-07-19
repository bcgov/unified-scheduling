import { getApiSchedulingAssignmentsEntries } from '@/api-access/generated/assignment/assignment';
import { getApiSchedulingAssignmentDefinitions } from '@/api-access/generated/assignment-definition/assignment-definition';
import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import type { AssignmentEntryResponse } from '@/api-access/generated/models/assignmentEntryResponse';
import type { CalendarContributionData, CalendarResourceBase } from '@/modules/calendar/calendarTypes';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { DateTime } from 'luxon';
import type { CalendarSchedulingEvent } from '../calendarSchedulingData';

export interface CalendarSchedulingAssignmentResource extends CalendarResourceBase {
  title: string;
  description?: string;
  subtitle?: string;
  meta?: CalendarMetaItem[];
  avatarText?: string;
  assignmentDefinitionId: number;
  locationId?: number;
  defaultStartTime?: string;
  defaultEndTime?: string;
  capacity?: number;
  assignmentCategoryTypeId?: number;
  assignmentCategoryTypeCode?: string;
  assignmentSubCategoryTypeId?: number;
  assignmentSubCategoryTypeCode?: string;
  entries?: CalendarSchedulingAssignmentResourceEntry[];
}

interface AssignmentDefinitionDisplayFields {
  description?: string | null;
  assignmentCategoryTypeDescription?: string | null;
  assignmentSubCategoryTypeDescription?: string | null;
  defaultCapacity?: number;
}

export interface CalendarSchedulingAssignmentResourceEntry {
  id: number;
  startAtUtc?: string | null;
  endAtUtc?: string | null;
  title?: string | null;
  description?: string | null;
  notes?: string | null;
  capacity?: number;
  linkedShiftEntryIds?: number[];
  assignedUserIds?: string[];
}

export interface CalendarSchedulingAssignmentContributionData {
  entries: AssignmentEntryResponse[];
  definitions: AssignmentDefinitionResponse[];
}

export const schedulingAssignmentContributionId = 'scheduling.assignment-events';

export const calendarSchedulingAssignmentsContribution: CalendarModuleContribution = {
  moduleId: 'scheduling',
  contributionId: schedulingAssignmentContributionId,
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.schedulingModule ?? true;
  },
  async load(context, options): Promise<CalendarContributionData> {
    if (!context.locationId) {
      return {
        moduleId: 'scheduling',
        contributionId: schedulingAssignmentContributionId,
        events: [],
        resources: [],
        data: {
          entries: [],
          definitions: [],
        } satisfies CalendarSchedulingAssignmentContributionData,
      };
    }

    const [entries, definitions] = await Promise.all([
      loadAssignmentEntries(context, options?.signal),
      loadAssignmentDefinitions(context, options?.signal),
    ]);

    return {
      moduleId: 'scheduling',
      contributionId: schedulingAssignmentContributionId,
      events: entries.flatMap(mapAssignmentEntryToCalendarEvent),
      resources: mapAssignmentResources(
        definitions.filter((definition) => assignmentDefinitionOverlapsDateRange(definition, context)),
        entries,
      ),
      data: {
        entries,
        definitions,
      } satisfies CalendarSchedulingAssignmentContributionData,
    };
  },
};

async function loadAssignmentEntries(
  context: Parameters<CalendarModuleContribution['load']>[0],
  signal?: AbortSignal,
): Promise<AssignmentEntryResponse[]> {
  const { data, error, execute } = getApiSchedulingAssignmentsEntries(
    {
      LocationId: context.locationId,
      StatusTypeCode: 'Active',
      StartAtUtc: toUtcStartOfDay(context.startDate, context.filters),
      EndAtUtc: toUtcEndOfDay(context.endDate, context.filters),
    },
    {
      fetchOptions: { signal },
      options: { immediate: false },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value ?? [];
}

async function loadAssignmentDefinitions(
  context: Parameters<CalendarModuleContribution['load']>[0],
  signal?: AbortSignal,
): Promise<AssignmentDefinitionResponse[]> {
  const { data, error, execute } = getApiSchedulingAssignmentDefinitions(
    { locationId: context.locationId },
    {
      fetchOptions: { signal },
      options: { immediate: false },
    },
  );

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value ?? [];
}

function mapAssignmentEntryToCalendarEvent(entry: AssignmentEntryResponse): CalendarSchedulingEvent[] {
  if (!entry.id || !entry.startAtUtc) {
    return [];
  }

  const assignmentDefinitionId = entry.assignmentDefinitionId;

  if (!assignmentDefinitionId) {
    return [];
  }

  const assignedUserIds = entry.assignedUserIds ?? [];
  const linkedShiftEntryIds = entry.linkedShiftEntryIds?.map(String) ?? [];

  return [
    {
      id: createAssignmentEntryEventId(entry.id),
      type: 'scheduling.assignment',
      sourceModule: 'calendar-assignment',
      title: entry.title || 'Assignment',
      description: entry.description ?? undefined,
      notes: entry.notes ?? undefined,
      color: entry.color ?? undefined,
      start: entry.startAtUtc,
      end: entry.endAtUtc ?? undefined,
      seriesStartAtUtc: entry.seriesStartAtUtc ?? undefined,
      seriesEndAtUtc: entry.seriesEndAtUtc ?? undefined,
      allDay: entry.allDay ?? false,
      isException: entry.isException ?? false,
      eventTypeCode: entry.eventTypeCode ?? undefined,
      statusTypeCode: entry.statusTypeCode ?? undefined,
      cancelledAt: entry.cancelledAt ?? undefined,
      cancelledByUserId: entry.cancelledByUserId ?? undefined,
      cancellationReason: entry.cancellationReason ?? undefined,
      timeZoneId: entry.timeZoneId ?? undefined,
      locationId: entry.locationId ?? undefined,
      resourceIds: [createAssignmentResourceId(assignmentDefinitionId)],
      metadata: {
        assignmentId: createAssignmentResourceId(assignmentDefinitionId),
        assignmentDefinitionId: String(assignmentDefinitionId),
        assignmentEntryId: String(entry.id),
        assignmentSeriesId: entry.assignmentSeriesId != null ? String(entry.assignmentSeriesId) : undefined,
        eventId: entry.eventId,
        capacity: entry.capacity,
        assignedCount: entry.assignedUserCount ?? assignedUserIds.length,
        assignedShiftIds: linkedShiftEntryIds,
        assignedUserIds,
        assignmentCategoryTypeId: entry.assignmentCategoryTypeId,
        assignmentCategoryTypeCode: entry.assignmentCategoryTypeCode ?? undefined,
        assignmentSubCategoryTypeId: entry.assignmentSubCategoryTypeId,
        assignmentSubCategoryTypeCode: entry.assignmentSubCategoryTypeCode ?? undefined,
      },
    },
  ];
}

function mapAssignmentResources(
  definitions: AssignmentDefinitionResponse[],
  entries: AssignmentEntryResponse[],
): CalendarSchedulingAssignmentResource[] {
  const resources = new Map<number, CalendarSchedulingAssignmentResource>();
  const entriesByDefinition = groupEntriesByDefinition(entries);

  for (const definition of definitions) {
    if (!definition.id || resources.has(definition.id)) {
      continue;
    }

    const displayFields = definition as AssignmentDefinitionResponse & AssignmentDefinitionDisplayFields;
    const title = definition.name || `Assignment ${definition.id}`;
    const subtitle = [
      displayFields.assignmentCategoryTypeDescription,
      displayFields.assignmentSubCategoryTypeDescription,
    ]
      .filter(Boolean)
      .join(' / ');

    resources.set(definition.id, {
      id: createAssignmentResourceId(definition.id),
      type: 'assignment',
      sourceModule: 'scheduling',
      label: title,
      title,
      description: displayFields.description ?? undefined,
      subtitle: subtitle || undefined,
      avatarText: toAvatarText(title),
      assignmentDefinitionId: definition.id,
      locationId: definition.locationId,
      defaultStartTime: definition.defaultStartTime ?? undefined,
      defaultEndTime: definition.defaultEndTime ?? undefined,
      capacity: displayFields.defaultCapacity,
      assignmentCategoryTypeId: definition.assignmentCategoryTypeId,
      assignmentCategoryTypeCode: undefined,
      assignmentSubCategoryTypeId: definition.assignmentSubCategoryTypeId,
      assignmentSubCategoryTypeCode: undefined,
      entries: entriesByDefinition.get(definition.id) ?? [],
    });
  }

  return Array.from(resources.values()).sort((left, right) => left.title.localeCompare(right.title));
}

function assignmentDefinitionOverlapsDateRange(
  definition: AssignmentDefinitionResponse,
  context: Parameters<CalendarModuleContribution['load']>[0],
) {
  const timeZoneId = resolveTimeZoneId(context.filters);
  const rangeStart = DateTime.fromISO(context.startDate, { zone: timeZoneId }).startOf('day');
  const rangeEnd = DateTime.fromISO(context.endDate, { zone: timeZoneId }).startOf('day');

  if (!rangeStart.isValid || !rangeEnd.isValid || rangeEnd <= rangeStart) {
    return true;
  }

  const effectiveDate = parseOptionalDateTime(definition.effectiveDateUtc, timeZoneId);
  const expiryDate = parseOptionalDateTime(definition.expiryDateUtc, timeZoneId);

  return (!effectiveDate || effectiveDate < rangeEnd) && (!expiryDate || expiryDate > rangeStart);
}

function parseOptionalDateTime(value: string | null | undefined, timeZoneId: string) {
  if (!value) {
    return null;
  }

  const dateTime = DateTime.fromISO(value, { setZone: true }).setZone(timeZoneId);
  return dateTime.isValid ? dateTime : null;
}

function groupEntriesByDefinition(entries: AssignmentEntryResponse[]) {
  const result = new Map<number, CalendarSchedulingAssignmentResourceEntry[]>();

  for (const entry of entries) {
    if (!entry.id || !entry.assignmentDefinitionId) {
      continue;
    }

    const items = result.get(entry.assignmentDefinitionId) ?? [];
    items.push({
      id: entry.id,
      startAtUtc: entry.startAtUtc,
      endAtUtc: entry.endAtUtc,
      title: entry.title,
      description: entry.description,
      notes: entry.notes,
      capacity: entry.capacity,
      linkedShiftEntryIds: entry.linkedShiftEntryIds ?? [],
      assignedUserIds: entry.assignedUserIds ?? [],
    });
    result.set(entry.assignmentDefinitionId, items);
  }

  return result;
}

function toUtcStartOfDay(date: string, filters: Record<string, unknown>) {
  return (
    DateTime.fromISO(date, { zone: resolveTimeZoneId(filters) })
      .startOf('day')
      .toUTC()
      .toISO() ?? undefined
  );
}

function toUtcEndOfDay(date: string, filters: Record<string, unknown>) {
  return (
    DateTime.fromISO(date, { zone: resolveTimeZoneId(filters) })
      .plus({ days: 1 })
      .startOf('day')
      .toUTC()
      .toISO() ?? undefined
  );
}

function resolveTimeZoneId(filters: Record<string, unknown>) {
  const timeZone = filters.timeZoneId ?? filters.timeZone;
  return typeof timeZone === 'string' && timeZone.trim() ? timeZone : 'America/Vancouver';
}

function createAssignmentResourceId(assignmentDefinitionId: number) {
  return `assignment-definition-${assignmentDefinitionId}`;
}

function createAssignmentEntryEventId(assignmentEntryId: number) {
  return `assignment-entry-${assignmentEntryId}`;
}

function toAvatarText(value: string) {
  return value
    .split(/\s+/)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase();
}
