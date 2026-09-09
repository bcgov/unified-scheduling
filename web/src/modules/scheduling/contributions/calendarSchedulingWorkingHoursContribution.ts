import { postApiSchedulingWorkingHoursQuery } from '@/api-access/generated/working-hours/working-hours';
import type { WorkingHoursResult } from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import type { CalendarContributionData } from '@/modules/calendar/calendarTypes';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import { addDays } from '@/utils/date';

export const schedulingWorkingHoursContributionId = 'scheduling.working-hours';

export interface CalendarSchedulingWorkingHoursContributionData {
  results: WorkingHoursResult[];
}

export const calendarSchedulingWorkingHoursContribution: CalendarModuleContribution = {
  moduleId: 'scheduling',
  contributionId: schedulingWorkingHoursContributionId,
  isAvailable(runtimeContext, queryContext) {
    const permissions = runtimeContext.permissions ?? [];
    return (
      queryContext.activeViewId === 'calendar.matrix-schedule' &&
      (runtimeContext.featureFlags.Scheduling?.enabled ?? true) &&
      permissions.includes(Permissions.AssignmentsView) &&
      permissions.includes(Permissions.ShiftsView)
    );
  },
  async load(context, options): Promise<CalendarContributionData> {
    if (context.locationId == null) {
      return buildContributionData([]);
    }

    const { data, error, execute } = postApiSchedulingWorkingHoursQuery(
      {
        startDate: context.startDate,
        endDate: addDays(context.endDate, -1),
        shiftLocationIds: [context.locationId],
        userIds: extractUserIds(context.filters),
      },
      {
        fetchOptions: { signal: options?.signal },
        options: { immediate: false },
      },
    );

    await execute();

    if (error.value) {
      throw error.value;
    }

    return buildContributionData(data.value ?? []);
  },
};

function buildContributionData(results: WorkingHoursResult[]): CalendarContributionData {
  return {
    moduleId: 'scheduling',
    contributionId: schedulingWorkingHoursContributionId,
    events: [],
    data: {
      results,
    } satisfies CalendarSchedulingWorkingHoursContributionData,
  };
}

function extractUserIds(filters: Record<string, unknown>) {
  const candidate = filters.userIds;

  if (!Array.isArray(candidate)) {
    return undefined;
  }

  const userIds = candidate.filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
  return userIds.length > 0 ? userIds : undefined;
}
