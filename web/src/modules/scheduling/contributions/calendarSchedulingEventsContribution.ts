import { getApiUsersUserIdActingPositions } from '@/api-access/generated/acting-positions/acting-positions';
import { postApiSchedulingCalendarEvents } from '@/api-access/generated/scheduling-calendar/scheduling-calendar';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { ActingPositionResponseDto, SchedulingCalendarRequest, UserResponse } from '@/api-access/generated/models';
import type { CalendarResourceBase } from '@/modules/calendar/calendarTypes';
import { CalendarContributionId, CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarSchedulingEvent, CalendarUser } from '../calendarSchedulingData';
import { resolveSchedulingTimeZoneFromFilters } from '../schedulingTimeZone';

export interface CalendarSchedulingUserResource extends CalendarResourceBase {
  title: string;
  subtitle?: string;
  meta?: CalendarMetaItem[];
  avatarText?: string;
}

interface CalendarSchedulingResourceData {
  users: UserResponse[];
  allUsersById: Map<string, UserResponse>;
  actingPositionsByUserId: Map<string, ActingPositionResponseDto[]>;
}

const resourceDataCache = new Map<string, Promise<CalendarSchedulingResourceData>>();

export function clearSchedulingCalendarResourceDataCache() {
  resourceDataCache.clear();
}

export const calendarSchedulingEventsContribution: CalendarModuleContribution = {
  moduleId: CalendarModuleId.Scheduling,
  contributionId: CalendarContributionId.SchedulingEvents,
  onDeactivate: clearSchedulingCalendarResourceDataCache,
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.Scheduling?.enabled ?? true;
  },
  async load(context, options) {
    if (context.locationId == null) {
      return {
        moduleId: CalendarModuleId.Scheduling,
        contributionId: CalendarContributionId.SchedulingEvents,
        events: [],
        resources: [],
      };
    }

    const userIds = extractUserIds(context.filters);

    const [data, resourceData] = await Promise.all([
      loadSchedulingCalendarData(
        {
          startDate: context.startDate,
          endDate: context.endDate,
          timeZoneId: resolveSchedulingTimeZoneFromFilters(context.filters),
          locationId: context.locationId,
          userIds,
        },
        options?.signal,
      ),
      loadSchedulingResourceData(context.locationId, options?.signal),
    ]);

    const events = data.events ?? [];
    const resourceUsers = filterResourceUsers(resourceData.users, userIds);

    return {
      moduleId: CalendarModuleId.Scheduling,
      contributionId: CalendarContributionId.SchedulingEvents,
      events: events.map<CalendarSchedulingEvent>((event) => {
        const assignedUserIds = event.assignedUserIds ?? [];

        return {
          id: event.id,
          type: event.type,
          sourceModule: event.sourceModule,
          title: event.title,
          description: event.description ?? undefined,
          notes: event.notes ?? undefined,
          color: event.color ?? undefined,
          start: event.start,
          end: event.end ?? undefined,
          seriesStartAtUtc: event.seriesStartAtUtc ?? undefined,
          seriesEndAtUtc: event.seriesEndAtUtc ?? undefined,
          allDay: event.allDay ?? false,
          isException: event.isException ?? false,
          eventTypeCode: event.eventTypeCode,
          statusTypeCode: event.statusTypeCode,
          cancelledAt: event.cancelledAt ?? undefined,
          cancelledByUserId: event.cancelledByUserId ?? undefined,
          cancellationReason: event.cancellationReason ?? undefined,
          timeZoneId: event.timeZoneId ?? undefined,
          locationId: event.locationId ?? undefined,
          resourceIds: event.resourceIds ?? [],
          metadata: {
            shiftEntryId: event.shiftEntryId == null ? undefined : String(event.shiftEntryId),
            shiftSeriesId: event.shiftSeriesId ?? undefined,
            assignmentEntryId: event.assignmentEntryId == null ? undefined : String(event.assignmentEntryId),
            assignmentSeriesId: event.assignmentSeriesId == null ? undefined : String(event.assignmentSeriesId),
            userIds: event.userIds ?? [],
            eventId: event.eventId,
            capacity: event.capacity ?? undefined,
            assignedCount: event.assignedUserCount ?? assignedUserIds.length,
            assignedShiftIds: (event.linkedShiftEntryIds ?? []).map(String),
            assignedUserIds,
            assignedUsers: assignedUserIds.flatMap((userId) => {
              const user = resourceData.allUsersById.get(userId);
              return user ? [mapUserToCalendarUser(user)] : [];
            }),
            categoryId: event.categoryId ?? undefined,
            categoryName: event.categoryName ?? undefined,
            subCategoryId: event.subCategoryId ?? undefined,
            subCategoryName: event.subCategoryName ?? undefined,
          },
        };
      }),
      resources: resourceUsers.map<CalendarSchedulingUserResource>((user) =>
        mapUserToCalendarSchedulingResource(user, resourceData.actingPositionsByUserId.get(user.id) ?? []),
      ),
    };
  },
};

function filterResourceUsers(users: UserResponse[], userIds?: string[]) {
  if (!userIds?.length) {
    return users;
  }

  const allowedUserIds = new Set(userIds);
  return users.filter((user) => allowedUserIds.has(user.id));
}

function mapUserToCalendarSchedulingResource(
  user: UserResponse,
  actingPositions: ActingPositionResponseDto[],
): CalendarSchedulingUserResource {
  const calendarUser = mapUserToCalendarUser(user);
  const meta = [
    ...mapActingPositionsToMeta(actingPositions),
    ...(user.badgeNumber ? [{ value: user.badgeNumber }] : []),
  ];

  return {
    id: user.id,
    type: 'user',
    sourceModule: 'scheduling',
    label: calendarUser.title,
    title: calendarUser.title,
    subtitle: calendarUser.subtitle,
    meta: meta.length ? meta : undefined,
    avatarText: toAvatarText(user.firstName, user.lastName, user.idirName),
  };
}

function mapUserToCalendarUser(user: UserResponse): CalendarUser {
  return {
    id: user.id,
    type: 'user',
    title: [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.idirName,
    subtitle: user.rank || undefined,
    avatarText: toAvatarText(user.firstName, user.lastName, user.idirName),
  };
}

function mapActingPositionsToMeta(actingPositions: ActingPositionResponseDto[]): CalendarMetaItem[] {
  const now = new Date();

  return actingPositions
    .filter((position) => isActingPositionCurrentlyValid(position, now))
    .map((position) => ({
      value: position.positionTypeDescription || position.positionTypeCode || 'Unknown',
    }));
}

function isActingPositionCurrentlyValid(position: ActingPositionResponseDto, now: Date) {
  const startAt = parseOptionalDate(position.startAtUtc);

  if (!startAt || startAt > now) {
    return false;
  }

  const endAt = parseOptionalDate(position.endAtUtc);
  if (endAt && endAt <= now) {
    return false;
  }

  const expiryAt = parseOptionalDate(position.expiryAtUtc);
  return !expiryAt || expiryAt > now;
}

function parseOptionalDate(value?: string | null) {
  if (!value) {
    return null;
  }

  const parsedTime = Date.parse(value);
  return Number.isNaN(parsedTime) ? null : new Date(parsedTime);
}

function toAvatarText(firstName?: string, lastName?: string, fallback?: string) {
  const initials = `${firstName?.trim().charAt(0) ?? ''}${lastName?.trim().charAt(0) ?? ''}`.trim().toUpperCase();

  if (initials) {
    return initials;
  }

  return fallback?.trim().slice(0, 2).toUpperCase() || undefined;
}

function extractUserIds(filters: Record<string, unknown>) {
  const candidate = filters.userIds;

  if (!Array.isArray(candidate)) {
    return undefined;
  }

  const userIds = candidate.filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
  return userIds.length > 0 ? userIds : undefined;
}

async function loadSchedulingCalendarData(request: SchedulingCalendarRequest, signal?: AbortSignal) {
  const { data, error, execute } = postApiSchedulingCalendarEvents(request, {
    fetchOptions: { signal },
    options: { immediate: false },
  });

  await execute();

  if (error.value) {
    throw error.value;
  }

  return data.value ?? {};
}

async function loadSchedulingCalendarUsers(locationId?: number, signal?: AbortSignal): Promise<UserResponse[]> {
  const { data, error, execute } = getApiUsers(
    {
      IsEnabled: true,
      LocationId: locationId,
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

async function loadSchedulingResourceData(
  locationId?: number,
  signal?: AbortSignal,
): Promise<CalendarSchedulingResourceData> {
  const cacheKey = createResourceDataCacheKey(locationId);
  const cachedResourceData = resourceDataCache.get(cacheKey);

  if (cachedResourceData) {
    return cachedResourceData;
  }

  const resourceData = loadSchedulingResourceDataFromApi(locationId, signal);
  resourceDataCache.set(cacheKey, resourceData);

  try {
    return await resourceData;
  } catch (error) {
    if (resourceDataCache.get(cacheKey) === resourceData) {
      resourceDataCache.delete(cacheKey);
    }

    throw error;
  }
}

async function loadSchedulingResourceDataFromApi(
  locationId?: number,
  signal?: AbortSignal,
): Promise<CalendarSchedulingResourceData> {
  const [users, allUsers] = await Promise.all([
    loadSchedulingCalendarUsers(locationId, signal),
    loadSchedulingCalendarUsers(undefined, signal),
  ]);
  const actingPositionsByUserId = await loadActingPositionsByUser(users, signal);

  return {
    users,
    allUsersById: new Map(allUsers.map((user) => [user.id, user])),
    actingPositionsByUserId,
  };
}

function createResourceDataCacheKey(locationId?: number) {
  return locationId == null ? 'all-locations' : String(locationId);
}

async function loadActingPositionsByUser(users: UserResponse[], signal?: AbortSignal) {
  const entries = await Promise.all(
    users.map(async (user) => {
      const { data, error, execute } = getApiUsersUserIdActingPositions(user.id, {
        fetchOptions: { signal },
        options: { immediate: false },
      });

      await execute();

      if (error.value) {
        throw error.value;
      }

      return [user.id, data.value ?? []] as const;
    }),
  );

  return new Map(entries);
}
