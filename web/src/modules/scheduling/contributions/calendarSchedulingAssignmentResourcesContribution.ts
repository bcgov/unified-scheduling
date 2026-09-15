import { getApiSchedulingAssignmentDefinitions } from '@/api-access/generated/assignment-definition/assignment-definition';
import type { AssignmentDefinitionResponse } from '@/api-access/generated/models/assignmentDefinitionResponse';
import type { CalendarContributionData, CalendarResourceBase } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import { assignmentDefinitionOverlapsCalendarDateRange } from '../assignmentDefinitionDateHelpers';
import { resolveSchedulingTimeZoneFromFilters } from '../schedulingTimeZone';
import { canViewAssignments } from '../calendarSchedulingPermissions';

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
  categoryId?: number;
  categoryName?: string;
  subCategoryId?: number;
  subCategoryName?: string;
}

export interface CalendarSchedulingAssignmentResourcesData {
  definitions: AssignmentDefinitionResponse[];
}

export const schedulingAssignmentResourcesContributionId = 'scheduling.assignment-resources';

export const calendarSchedulingAssignmentResourcesContribution: CalendarModuleContribution = {
  moduleId: 'scheduling',
  contributionId: schedulingAssignmentResourcesContributionId,
  isAvailable(runtimeContext) {
    return (runtimeContext.featureFlags.Scheduling?.enabled ?? true) && canViewAssignments(runtimeContext);
  },
  async load(context, options): Promise<CalendarContributionData> {
    if (!context.locationId) {
      return emptyContribution();
    }

    const definitions = await loadAssignmentDefinitions(context.locationId, options?.signal);
    const visibleDefinitions = definitions.filter((definition) =>
      assignmentDefinitionOverlapsCalendarDateRange(
        definition,
        context.startDate,
        context.endDate,
        resolveSchedulingTimeZoneFromFilters(context.filters),
      ),
    );

    return {
      moduleId: 'scheduling',
      contributionId: schedulingAssignmentResourcesContributionId,
      events: [],
      resources: mapAssignmentResources(visibleDefinitions),
      data: { definitions } satisfies CalendarSchedulingAssignmentResourcesData,
    };
  },
};

function emptyContribution(): CalendarContributionData {
  return {
    moduleId: 'scheduling',
    contributionId: schedulingAssignmentResourcesContributionId,
    events: [],
    resources: [],
    data: { definitions: [] } satisfies CalendarSchedulingAssignmentResourcesData,
  };
}

async function loadAssignmentDefinitions(locationId: number, signal?: AbortSignal) {
  const { data, error, execute } = getApiSchedulingAssignmentDefinitions(
    { locationId },
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

function mapAssignmentResources(definitions: AssignmentDefinitionResponse[]): CalendarSchedulingAssignmentResource[] {
  return definitions
    .filter((definition): definition is AssignmentDefinitionResponse & { id: number } => Boolean(definition.id))
    .map((definition) => {
      const title = definition.name || `Assignment ${definition.id}`;
      const subtitle = [definition.categoryName, definition.subCategoryName].filter(Boolean).join(' / ');

      return {
        id: createAssignmentResourceId(definition.id),
        type: 'assignment',
        sourceModule: 'scheduling',
        label: title,
        title,
        description: definition.description ?? undefined,
        subtitle: subtitle || undefined,
        avatarText: toAvatarText(title),
        assignmentDefinitionId: definition.id,
        locationId: definition.locationId,
        defaultStartTime: definition.defaultStartTime ?? undefined,
        defaultEndTime: definition.defaultEndTime ?? undefined,
        capacity: definition.defaultCapacity,
        categoryId: definition.categoryId,
        categoryName: definition.categoryName,
        subCategoryId: definition.subCategoryId,
        subCategoryName: definition.subCategoryName,
      };
    })
    .sort((left, right) => left.title.localeCompare(right.title));
}

function createAssignmentResourceId(assignmentDefinitionId: number) {
  return `assignment-definition-${assignmentDefinitionId}`;
}

function toAvatarText(value: string) {
  return value
    .split(/\s+/)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase();
}
