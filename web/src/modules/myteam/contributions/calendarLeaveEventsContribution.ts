import { postApiLeaveCalendarEvents } from '@/api-access/generated/leave-calendar/leave-calendar';
import type { LeaveCalendarEventResponse } from '@/api-access/generated/models';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { CalendarContributionId, CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import { addDays, toApiDateString, toCalendarDateOnly } from '@/utils/date';

function extractUserIds(filters: Record<string, unknown>): string[] | undefined {
  const userIds = filters.userIds;
  return Array.isArray(userIds) ? userIds.filter((id): id is string => typeof id === 'string') : undefined;
}

function mapLeaveEventToCalendarEventBase(event: LeaveCalendarEventResponse): CalendarEventBase {
  const start = event.allDay ? (toCalendarDateOnly(event.start) ?? event.start) : event.start;
  const end = event.allDay ? toCalendarDateOnly(event.end ?? undefined) : (event.end ?? undefined);

  return {
    id: event.id,
    type: event.type,
    sourceModule: event.sourceModule,
    title: event.title,
    description: event.description ?? undefined,
    notes: event.notes ?? undefined,
    color: event.color ?? undefined,
    start,
    end,
    allDay: event.allDay ?? false,
    eventTypeCode: event.eventTypeCode,
    statusTypeCode: event.statusTypeCode,
    cancelledAt: event.cancelledAt ?? undefined,
    cancelledByUserId: event.cancelledByUserId ?? undefined,
    cancellationReason: event.cancellationReason ?? undefined,
    timeZoneId: event.timeZoneId ?? undefined,
    resourceIds: [event.userId],
  };
}

export const calendarLeaveEventsContribution: CalendarModuleContribution = {
  moduleId: CalendarModuleId.UserManagement,
  contributionId: CalendarContributionId.LeaveEvents,
  isAvailable(runtimeContext) {
    const calendarEnabled = runtimeContext.featureFlags.Calendar?.enabled ?? false;
    const leaveEnabled = runtimeContext.featureFlags.UserManagement?.leave?.enabled ?? false;
    return calendarEnabled && leaveEnabled;
  },
  async load(context, options) {
    const { data, error, execute } = postApiLeaveCalendarEvents(
      {
        startDate: toApiDateString(context.startDate),
        // Calendar view ranges end exclusively; the API accepts an inclusive date-only range.
        endDate: toApiDateString(addDays(context.endDate, -1)),
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

    return {
      moduleId: CalendarModuleId.UserManagement,
      contributionId: CalendarContributionId.LeaveEvents,
      events: (data.value?.events ?? []).map<CalendarEventBase>(mapLeaveEventToCalendarEventBase),
    };
  },
};
