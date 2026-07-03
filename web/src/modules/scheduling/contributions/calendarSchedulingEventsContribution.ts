import { getApiUsersUserIdActingPositions } from '@/api-access/generated/acting-positions/acting-positions';
import { postApiSchedulingCalendarEvents } from '@/api-access/generated/scheduling-calendar/scheduling-calendar';
import { getApiUsers } from '@/api-access/generated/users/users';
import type { ActingPositionResponseDto, SchedulingCalendarRequest, UserResponse } from '@/api-access/generated/models';
import type { CalendarResourceBase } from '@/modules/calendar/calendarTypes';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarSchedulingEvent } from '../calendarSchedulingData';

export interface CalendarSchedulingUserResource extends CalendarResourceBase {
  title: string;
  subtitle?: string;
  meta?: CalendarMetaItem[];
  avatarText?: string;
}

interface CalendarSchedulingResourceData {
  users: UserResponse[];
  actingPositionsByUserId: Map<string, ActingPositionResponseDto[]>;
}

const resourceDataCache = new Map<string, Promise<CalendarSchedulingResourceData>>();

export const calendarSchedulingEventsContribution: CalendarModuleContribution = {
  moduleId: 'scheduling',
  contributionId: 'scheduling.shift-events',
  isAvailable(runtimeContext) {
    return runtimeContext.featureFlags.calendarSchedulingModule ?? true;
  },
  async load(context, options) {
    const filters = toSchedulingFilters(context.filters);
    const userIds = extractUserIds(context.filters);

    const [data, resourceData] = await Promise.all([
      loadSchedulingCalendarData(
        {
          startDate: context.startDate,
          endDate: context.endDate,
          timeZoneId: resolveTimeZoneId(context.filters),
          locationId: context.locationId,
          userIds,
          filters,
        },
        options?.signal,
      ),
      loadSchedulingResourceData(context.locationId, options?.signal),
    ]);

    const events = data.events ?? [];
    const resourceUsers = filterResourceUsers(resourceData.users, userIds);

    return {
      moduleId: data.moduleId ?? 'scheduling',
      contributionId: data.contributionId ?? 'scheduling.shift-events',
      events: events.map<CalendarSchedulingEvent>((event) => ({
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
        isConflict: eventHasConflict(event),
        eventTypeCode: event.eventTypeCode,
        statusTypeCode: event.statusTypeCode,
        cancelledAt: event.cancelledAt ?? undefined,
        cancelledByUserId: event.cancelledByUserId ?? undefined,
        cancellationReason: event.cancellationReason ?? undefined,
        timeZoneId: event.timeZoneId ?? undefined,
        locationId: event.locationId ?? undefined,
        resourceIds: event.resourceIds ?? [],
        metadata: {
          shiftEntryId: event.shiftEntryId === undefined ? undefined : String(event.shiftEntryId),
          userIds: event.userIds ?? [],
          eventId: event.eventId,
          shiftSeriesId: event.shiftSeriesId ?? undefined,
        },
      })),
      resources: resourceUsers.map<CalendarSchedulingUserResource>((user) =>
        mapUserToCalendarSchedulingResource(user, resourceData.actingPositionsByUserId.get(user.id) ?? []),
      ),
    };
  },
};

function eventHasConflict(event: unknown) {
  return typeof event === 'object' && event !== null && 'isConflict' in event && event.isConflict === true;
}

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
  const title = [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.idirName;
  const subtitle = user.rank ?? '';
  const meta = [
    ...mapActingPositionsToMeta(actingPositions),
    ...(user.badgeNumber ? [{ value: user.badgeNumber }] : []),
  ];

  return {
    id: user.id,
    type: 'user',
    sourceModule: 'scheduling',
    label: title,
    title,
    subtitle: subtitle || undefined,
    meta: meta.length ? meta : undefined,
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

function toSchedulingFilters(filters: Record<string, unknown>) {
  const entries = Object.entries(filters).flatMap(([key, value]) => {
    if (typeof value !== 'string') {
      return [];
    }

    const trimmed = value.trim();
    return trimmed ? [[key, trimmed] as const] : [];
  });

  return entries.length > 0 ? Object.fromEntries(entries) : undefined;
}

function resolveTimeZoneId(filters: Record<string, unknown>) {
  const timeZone = filters.timeZoneId ?? filters.timeZone;
  return typeof timeZone === 'string' && timeZone.trim() ? timeZone : undefined;
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
  const users = await loadSchedulingCalendarUsers(locationId, signal);
  const actingPositionsByUserId = await loadActingPositionsByUser(users, signal);

  return {
    users,
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
