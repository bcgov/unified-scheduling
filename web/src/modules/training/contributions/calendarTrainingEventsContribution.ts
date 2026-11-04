import { Permissions } from '@/api-access/generated/models';
import { useFetchAPI } from '@/api-access/useFetchAPI';
import type { CalendarEventBase, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import { useLoanOutSchedulingUsers } from '@/modules/loanout/api/loanOutApi';
import { canViewLoanOut } from '@/modules/scheduling/calendarSchedulingPermissions';
import {
  resolveSchedulingTimeZoneFromFilters,
  resolveSchedulingTimeZoneId,
} from '@/modules/scheduling/schedulingTimeZone';
import { useLocationsStore } from '@/stores/LocationsStore';
import { addDays, toApiDateString, toCalendarDateOnly } from '@/utils/date';

export const trainingCalendarContributionId = 'training.events';

export interface TrainingCalendarEvent extends CalendarEventBase {
  userTrainingId: number;
  userId: string;
  trainingId: number;
}

export function isTrainingCalendarEvent(event: unknown): event is TrainingCalendarEvent {
  if (typeof event !== 'object' || event === null) {
    return false;
  }

  const candidate = event as Partial<TrainingCalendarEvent>;
  return (
    candidate.sourceModule === 'training' &&
    candidate.type === 'training.user-training' &&
    typeof candidate.id === 'string' &&
    typeof candidate.userTrainingId === 'number' &&
    typeof candidate.userId === 'string'
  );
}

interface TrainingCalendarEventResponse {
  id: string;
  userTrainingId: number;
  userId: string;
  trainingId: number;
  type: string;
  sourceModule: string;
  title: string;
  start: string;
  end: string;
  allDay: boolean;
  isReadOnly: boolean;
  locationId?: number | null;
}

const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function validUserIds(filters: Record<string, unknown>): string[] | undefined {
  const candidate = filters.userIds;
  if (!Array.isArray(candidate)) return undefined;

  const ids = candidate.filter(
    (value): value is string =>
      typeof value === 'string' &&
      guidPattern.test(value) &&
      value.toLowerCase() !== '00000000-0000-0000-0000-000000000000',
  );
  return ids.length ? ids : undefined;
}

async function loadTrainingEvents(context: CalendarQueryContext, userIds?: string[], signal?: AbortSignal) {
  const { data, error, execute, abort } = useFetchAPI<TrainingCalendarEventResponse[]>(
    {
      url: '/api/training/calendar/events',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: {
        startDate: toApiDateString(context.startDate),
        endDate: toApiDateString(addDays(context.endDate, -1)),
        locationId: context.locationId,
        userIds,
      },
    },
    { fetchOptions: { signal }, options: { immediate: false } },
  );

  if (signal?.aborted) throw new DOMException('Training calendar request was aborted.', 'AbortError');
  const abortRequest = () => abort();
  signal?.addEventListener('abort', abortRequest, { once: true });
  try {
    await execute();
  } finally {
    signal?.removeEventListener('abort', abortRequest);
  }
  if (signal?.aborted) throw new DOMException('Training calendar request was aborted.', 'AbortError');
  if (error.value) throw error.value;
  return data.value ?? [];
}

export const calendarTrainingEventsContribution: CalendarModuleContribution = {
  moduleId: 'training',
  contributionId: trainingCalendarContributionId,
  isAvailable(runtimeContext) {
    return (
      runtimeContext.featureFlags.Calendar?.enabled === true &&
      runtimeContext.featureFlags.Training?.enabled === true &&
      (runtimeContext.permissions?.includes(Permissions.UserTrainingsView) ?? false)
    );
  },
  async load(context, options) {
    const userIds = validUserIds(context.filters);
    if (Array.isArray(context.filters.userIds) && context.filters.userIds.length > 0 && !userIds) {
      return { moduleId: 'training', contributionId: trainingCalendarContributionId, events: [] };
    }

    let events = await loadTrainingEvents(context, userIds, options?.signal);
    const runtime = options?.runtimeContext;
    if (
      !userIds &&
      context.locationId != null &&
      runtime?.featureFlags.Scheduling?.enabled &&
      canViewLoanOut(runtime)
    ) {
      const borrowedUsers = useLoanOutSchedulingUsers(
        {
          startDate: context.startDate,
          endDate: addDays(context.endDate, -1),
          locationId: context.locationId,
          timeZoneId: resolveSchedulingTimeZoneId(
            useLocationsStore().entitiesMap[context.locationId]?.timezone,
            resolveSchedulingTimeZoneFromFilters(context.filters),
          ),
        },
        options?.signal,
      );
      await borrowedUsers.execute();
      if (options?.signal?.aborted) throw new DOMException('Training calendar request was aborted.', 'AbortError');
      if (borrowedUsers.error.value) throw borrowedUsers.error.value;
      if (!borrowedUsers.data.value) throw new Error('Failed to load loan-out users.');

      const borrowedUserIds = [...new Set(borrowedUsers.data.value.map((user) => user.id))];
      if (borrowedUserIds.length) {
        const borrowedEvents = await loadTrainingEvents(context, borrowedUserIds, options?.signal);
        events = [...new Map([...events, ...borrowedEvents].map((event) => [event.id, event])).values()];
      }
    }

    return {
      moduleId: 'training',
      contributionId: trainingCalendarContributionId,
      events: events.map<TrainingCalendarEvent>((event) => ({
        id: event.id,
        userTrainingId: event.userTrainingId,
        userId: event.userId,
        trainingId: event.trainingId,
        type: event.type,
        sourceModule: event.sourceModule,
        title: event.title,
        start: toCalendarDateOnly(event.start) ?? event.start,
        end: toCalendarDateOnly(event.end) ?? event.end,
        allDay: event.allDay,
        isReadOnly: event.isReadOnly,
        eventTypeCode: 'training',
        locationId: event.locationId ?? undefined,
        resourceIds: [event.userId],
      })),
    };
  },
};
