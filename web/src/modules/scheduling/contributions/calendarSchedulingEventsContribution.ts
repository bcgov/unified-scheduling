import { getApiUsersUserIdActingPositions } from '@/api-access/generated/acting-positions/acting-positions';
import { postApiSchedulingCalendarEvents } from '@/api-access/generated/scheduling-calendar/scheduling-calendar';
import type { ActingPositionResponseDto, SchedulingCalendarRequest, UserResponse } from '@/api-access/generated/models';
import type { CalendarResourceBase } from '@/modules/calendar/calendarTypes';
import { CalendarContributionId, CalendarModuleId } from '@/modules/calendar/calendarIdentifiers';
import type { CalendarModuleContribution } from '@/modules/calendar/registry/calendarRegistryTypes';
import type { CalendarMatrixMetaItem as CalendarMetaItem } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import type { CalendarSchedulingEvent, CalendarUser } from '../calendarSchedulingData';
import { resolveSchedulingTimeZoneFromFilters } from '../schedulingTimeZone';
import { canViewAssignments, canViewShifts } from '../calendarSchedulingPermissions';
import { useUsersStore } from '@/stores/Users';

export interface CalendarSchedulingUserResource extends CalendarResourceBase {
  title: string;
  subtitle?: string;
  meta?: CalendarMetaItem[];
  avatarText?: string;
}

interface CalendarSchedulingResourceData {
  users: UserResponse[];
  getUserById(userId: string): UserResponse | undefined;
  actingPositionsByUserId: Map<string, ActingPositionResponseDto[]>;
}

const actingPositionsByUserIdCache = new Map<string, ActingPositionResponseDto[]>();

export const calendarSchedulingEventsContribution: CalendarModuleContribution = {
  moduleId: CalendarModuleId.Scheduling,
  contributionId: CalendarContributionId.SchedulingEvents,
  isAvailable(runtimeContext) {
    return (
      (runtimeContext.featureFlags.Scheduling?.enabled ?? true) &&
      (canViewShifts(runtimeContext) || canViewAssignments(runtimeContext))
    );
  },
  onDeactivate() {
    actingPositionsByUserIdCache.clear();
  },
  async load(context, options) {
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
            shiftEntryId: event.shiftEntryId == null ? undefined : String(event.shiftEntryId),
            shiftSeriesId: event.shiftSeriesId ?? undefined,
            assignmentEntryId: event.assignmentEntryId == null ? undefined : String(event.assignmentEntryId),
            assignmentSeriesId: event.assignmentSeriesId == null ? undefined : String(event.assignmentSeriesId),
            assignmentDefinitionId: resolveAssignmentDefinitionId(event),
            userIds: event.userIds ?? [],
            eventId: event.eventId,
            capacity: event.capacity ?? undefined,
            assignedCount: event.assignedUserCount ?? assignedUserIds.length,
            assignedShiftIds: (event.linkedShiftEntryIds ?? []).map(String),
            assignedUserIds,
            assignedUsers: assignedUserIds.flatMap((userId) => {
              const user = resourceData.getUserById(userId);
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

function resolveAssignmentDefinitionId(event: unknown) {
  if (!event || typeof event !== 'object' || !('assignmentDefinitionId' in event)) {
    return undefined;
  }

  const assignmentDefinitionId = Number(event.assignmentDefinitionId);
  return Number.isInteger(assignmentDefinitionId) && assignmentDefinitionId > 0
    ? String(assignmentDefinitionId)
    : undefined;
}

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

function mapUserToCalendarUser(user: UserResponse): CalendarUser {
  const title = [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.idirName;

  return {
    id: user.id,
    type: 'user',
    title,
    subtitle: user.rank ?? undefined,
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

async function loadSchedulingResourceData(
  locationId?: number,
  signal?: AbortSignal,
): Promise<CalendarSchedulingResourceData> {
  if (!locationId) {
    return {
      users: [],
      getUserById: () => undefined,
      actingPositionsByUserId: new Map(),
    };
  }

  const usersStore = useUsersStore();
  const [users] = await Promise.all([
    usersStore.ensureUsersForLocation(locationId, signal),
    usersStore.ensureAllUsers(signal),
  ]);
  const actingPositionsByUserId = await loadActingPositionsByUser(users, signal);

  return {
    users,
    getUserById: usersStore.getUserById,
    actingPositionsByUserId,
  };
}

async function loadActingPositionsByUser(users: UserResponse[], signal?: AbortSignal) {
  const entries = await Promise.all(
    users.map(async (user) => {
      if (actingPositionsByUserIdCache.has(user.id)) {
        return [user.id, actingPositionsByUserIdCache.get(user.id) ?? []] as const;
      }

      const { data, error, execute } = getApiUsersUserIdActingPositions(user.id, {
        fetchOptions: { signal },
        options: { immediate: false },
      });

      await execute();

      if (error.value) {
        throw error.value;
      }

      const actingPositions = data.value ?? [];
      actingPositionsByUserIdCache.set(user.id, actingPositions);
      return [user.id, actingPositions] as const;
    }),
  );

  return new Map(entries);
}
