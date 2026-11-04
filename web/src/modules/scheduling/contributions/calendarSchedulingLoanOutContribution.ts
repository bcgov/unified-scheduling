import type { LoanOutCalendarUser, SchedulingCalendarEventResponse } from '@/api-access/generated/models';
import type {
  CalendarModuleContribution,
  CalendarContributionLoadOptions,
} from '@/modules/calendar/registry/calendarRegistryTypes';
import { useLoanOutSchedulingUsers } from '@/modules/loanout/api/loanOutApi';
import { useCalendarAlertStore } from '@/modules/calendar/calendarAlertStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import { DateTime } from 'luxon';
import type { CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import { getLeaveCalendarEvents, type LeaveCalendarEvent } from '@/modules/leave/api/leaveApi';
import type { CalendarSchedulingUserResource } from './calendarSchedulingEventsContribution';
import { mapSchedulingCalendarEvent } from './calendarSchedulingEventsContribution';
import { formatUserName } from '@/utils/user';
import { canViewLeave, canViewLoanOut } from '../calendarSchedulingPermissions';
import { resolveSchedulingTimeZoneFromFilters, resolveSchedulingTimeZoneId } from '../schedulingTimeZone';

export const schedulingLoanOutContributionId = 'scheduling.loanout';
const borrowedLeaveAlertId = 'scheduling.loanout.leave-unavailable';

export const calendarSchedulingLoanOutContribution: CalendarModuleContribution = {
  moduleId: 'loanout',
  contributionId: schedulingLoanOutContributionId,
  isAvailable: (runtimeContext) =>
    Boolean(runtimeContext.featureFlags.Scheduling?.enabled) && canViewLoanOut(runtimeContext),
  onDeactivate() {
    useCalendarAlertStore().clearAlert(borrowedLeaveAlertId);
  },
  async load(context, options) {
    const locations = useLocationsStore();
    const timeZone = resolveSchedulingTimeZoneId(
      context.locationId == null ? undefined : locations.entitiesMap[context.locationId]?.timezone,
      resolveSchedulingTimeZoneFromFilters(context.filters),
    );
    const loanOutUsers = await loadLoanOutUsers(
      {
        startDate: context.startDate,
        endDate: getInclusiveEndDate(context.endDate),
        locationId: context.locationId,
        timeZoneId: timeZone,
      },
      options?.signal,
    );
    const userFilter = Array.isArray(context.filters.userIds) ? context.filters.userIds : [];
    const selectedLoanOutUsers = userFilter.length
      ? loanOutUsers.filter((user) => userFilter.includes(user.id))
      : loanOutUsers;
    const resources = selectedLoanOutUsers.map(mapLoanOutUserToResource);
    const leave = await loadLoanOutLeaveEvents(context, resources, options);
    if (options?.signal?.aborted) throw new DOMException('Loan-out request was aborted.', 'AbortError');
    return {
      moduleId: 'loanout',
      contributionId: schedulingLoanOutContributionId,
      events: leave.map((event) => ({
        ...mapSchedulingCalendarEvent(event),
        isReadOnly: true,
      })),
      resources,
    };
  },
};

async function loadLoanOutUsers(
  request: {
    startDate: string;
    endDate: string;
    locationId?: number;
    timeZoneId: string;
  },
  signal?: AbortSignal,
): Promise<LoanOutCalendarUser[]> {
  const result = useLoanOutSchedulingUsers(request, signal);

  await result.execute();

  if (result.error.value) throw result.error.value;
  if (!result.data.value) throw new Error('Failed to load loan-out users.');
  return result.data.value;
}

function getInclusiveEndDate(exclusiveEndDate: string) {
  const inclusiveEndDate = DateTime.fromISO(exclusiveEndDate, { zone: 'utc' }).minus({ days: 1 }).toISODate();
  if (!inclusiveEndDate) {
    throw new Error('Failed to resolve the loan-out date range.');
  }
  return inclusiveEndDate;
}

function mapLoanOutUserToResource(user: LoanOutCalendarUser) {
  const title = formatUserName(user);
  const initials = `${user.firstName.trim().charAt(0)}${user.lastName.trim().charAt(0)}`.toUpperCase();

  return {
    id: user.id,
    type: 'user',
    sourceModule: 'loanout',
    label: title,
    title,
    subtitle: user.rank ?? undefined,
    meta: user.badgeNumber ? [{ value: user.badgeNumber }] : undefined,
    avatarText: initials || user.idirName.trim().slice(0, 2).toUpperCase() || undefined,
    loanOutMetaData: validateLoanOutMetaData(user),
  };
}

function validateLoanOutMetaData(user: LoanOutCalendarUser) {
  return (user.loanOutMetaData ?? []).map((metadata) => {
    if (!metadata.startAtUtc || !DateTime.fromISO(metadata.startAtUtc, { setZone: true }).isValid) {
      throw new Error(`Loan-out metadata for user ${user.id} is missing a valid start time.`);
    }

    if (metadata.endAtUtc) {
      const endAt = DateTime.fromISO(metadata.endAtUtc, { setZone: true });
      if (!endAt.isValid || endAt.toMillis() < DateTime.fromISO(metadata.startAtUtc).toMillis()) {
        throw new Error(`Loan-out metadata for user ${user.id} has an invalid end time.`);
      }
    }

    return metadata;
  });
}
export async function loadLoanOutLeaveEvents(
  context: CalendarQueryContext,
  resources: CalendarSchedulingUserResource[],
  options?: CalendarContributionLoadOptions,
): Promise<SchedulingCalendarEventResponse[]> {
  const alerts = useCalendarAlertStore();
  alerts.clearAlert(borrowedLeaveAlertId);
  const runtime = options?.runtimeContext;
  if (
    !runtime ||
    runtime.featureFlags.Leave?.enabled !== true ||
    !canViewLeave(runtime) ||
    !canViewLoanOut(runtime) ||
    context.locationId == null
  )
    return [];
  const userIds = [...new Set(resources.map((user) => user.id))];
  if (!userIds.length) return [];
  const zone = resolveSchedulingTimeZoneId(
    useLocationsStore().entitiesMap[context.locationId]?.timezone,
    resolveSchedulingTimeZoneFromFilters(context.filters),
  );
  const startAtUtc = DateTime.fromISO(context.startDate, { zone }).toUTC().toISO();
  const endAtUtc = DateTime.fromISO(context.endDate, { zone }).toUTC().toISO();
  if (!startAtUtc || !endAtUtc) throw new Error('Invalid leave calendar date range.');
  let events: LeaveCalendarEvent[];
  try {
    events = await getLeaveCalendarEvents({ startAtUtc, endAtUtc, userIds }, options?.signal);
  } catch (error) {
    if (options?.signal?.aborted || (error instanceof Error && error.name === 'AbortError')) throw error;
    alerts.setAlert({
      id: borrowedLeaveAlertId,
      severity: 'warning',
      source: schedulingLoanOutContributionId,
      message:
        'Leave for borrowed employees could not be loaded. Other calendar data is still available. Refresh to retry.',
    });
    return [];
  }
  return projectLoanOutLeave(events, resources, startAtUtc, endAtUtc);
}

// Project the same leave identity into the applicable location windows; never copy database records.
export function projectLoanOutLeave(
  events: LeaveCalendarEvent[],
  borrowed: CalendarSchedulingUserResource[],
  rangeStart: string,
  rangeEnd: string,
): SchedulingCalendarEventResponse[] {
  const result: SchedulingCalendarEventResponse[] = [];
  for (const event of events) {
    const projections = new Map<string, { start: number; end: number; users: string[] }>();
    for (const userId of event.userIds ?? []) {
      const windows = borrowed.find((user) => user.id === userId)?.loanOutMetaData ?? [];
      const overlaps = windows
        .flatMap((window) => {
          if (!window.startAtUtc || !window.endAtUtc) return [];
          const start = Math.max(Date.parse(event.start), Date.parse(window.startAtUtc), Date.parse(rangeStart));
          const end = Math.min(Date.parse(event.end ?? event.start), Date.parse(window.endAtUtc), Date.parse(rangeEnd));
          return end > start ? [{ start, end }] : [];
        })
        .sort((a, b) => a.start - b.start);
      const merged: Array<{ start: number; end: number }> = [];
      for (const overlap of overlaps) {
        const last = merged.at(-1);
        if (last && overlap.start <= last.end) last.end = Math.max(last.end, overlap.end);
        else merged.push({ ...overlap });
      }
      for (const interval of merged) {
        const key = `${interval.start}:${interval.end}`;
        const projection = projections.get(key) ?? { ...interval, users: [] };
        if (!projection.users.includes(userId)) projection.users.push(userId);
        projections.set(key, projection);
      }
    }
    for (const [key, projection] of projections) {
      result.push({
        ...event,
        id: `${event.id}.location.${key}`,
        start: new Date(projection.start).toISOString(),
        end: new Date(projection.end).toISOString(),
        userIds: projection.users,
        resourceIds: projection.users,
      });
    }
  }
  return result;
}
