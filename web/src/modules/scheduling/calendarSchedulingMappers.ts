import {
  addDays,
  DATE_FORMAT,
  formatCalendarEventTimeRange,
  formatLocalDateOnly,
  hasExplicitTimeZoneOffset,
  parseLocalDateOnly,
  startOfWeek,
  toCalendarDateOnly,
  toDateTime,
} from '@/utils/date';
import type { CalendarPeriod } from '@/modules/calendar/calendarStore';
import { CalendarContributionId } from '@/modules/calendar/calendarIdentifiers';
import { selectContribution } from '@/modules/calendar/calendarSelectors';
import type { CalendarDataResponse, CalendarEventBase, CalendarQueryContext } from '@/modules/calendar/calendarTypes';
import {
  CalendarMatrixActionType,
  type CalendarMatrixCell,
  type CalendarMatrixCellHeader,
  type CalendarMatrixDay,
  type CalendarMatrixActionDisplay,
  type CalendarMatrixResource,
  type CalendarMatrixViewModel,
} from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import type { CalendarSchedulingUserResource } from './contributions/calendarSchedulingEventsContribution';
import { calendarSchedulingActionIds } from './calendarSchedulingActionIds';
import { mdiAlertCircle, mdiCalendarSync } from '@mdi/js';

const defaultCalendarSchedulingTimeZone = 'America/Vancouver';
const schedulingShiftContributionId = CalendarContributionId.SchedulingShiftEvents;

export function buildCalendarSchedulingViewModel(
  response: CalendarDataResponse,
  context: CalendarQueryContext,
  period: CalendarPeriod,
): CalendarMatrixViewModel {
  if (period === 'month') {
    return {
      unsupportedMessage: 'Not supported',
      days: [],
      primaryColumn: {
        label: 'TEAM',
        resources: [],
      },
      cells: [],
    };
  }

  const days = buildDays(context.startDate, period);
  const timeZone = resolveMatrixTimeZone(context);
  const shiftEvents = selectSchedulingShiftEvents(response);
  const resources = buildUserResourceRows(response);
  const cells: CalendarMatrixCell[] = [];

  for (const user of resources) {
    for (const day of days) {
      const userShiftEvents = shiftEvents.filter(
        (event) => event.resourceIds?.includes(user.id) && isEventOnMatrixDate(event, day.date, timeZone),
      );

      cells.push({
        resourceId: user.id,
        date: day.date,
        headers: userShiftEvents.map((event) => buildCellHeader(event, timeZone)),
        groups: [
          {
            id: 'assignments',
            variant: 'primary',
            showColorBar: true,
            events: [],
          },
        ],
      });
    }
  }

  return {
    days,
    timeZone,
    primaryColumn: {
      label: 'TEAM',
      resources,
    },
    cells,
  };
}

function buildUserResourceRows(response: CalendarDataResponse): CalendarMatrixResource[] {
  return selectSchedulingUserResources(response).map((user) => ({
    id: user.id,
    type: user.type,
    title: user.title,
    subtitle: user.subtitle,
    meta: user.meta,
    avatarText: user.avatarText,
    action: {
      actionId: calendarSchedulingActionIds.addResource,
      label: '+',
      ariaLabel: `Add resource action for ${user.title}`,
    },
  }));
}

function selectSchedulingShiftEvents(response: CalendarDataResponse) {
  const contribution = selectContribution(response, schedulingShiftContributionId);

  if (!contribution) {
    return [];
  }

  return contribution.events.filter(isCalendarSchedulingEvent);
}

function selectSchedulingUserResources(response: CalendarDataResponse): CalendarSchedulingUserResource[] {
  const contribution = selectContribution(response, schedulingShiftContributionId);

  if (!contribution?.resources) {
    return [];
  }

  return contribution.resources.flatMap((resource) => (isCalendarSchedulingUserResource(resource) ? [resource] : []));
}

function isCalendarSchedulingUserResource(resource: {
  id: string;
  type: string;
}): resource is CalendarSchedulingUserResource {
  return 'title' in resource && typeof resource.title === 'string';
}

function buildCellHeader(
  event: CalendarEventBase,
  timeZone = defaultCalendarSchedulingTimeZone,
): CalendarMatrixCellHeader {
  return {
    id: event.id,
    text: formatCalendarEventTimeRange(event.start, event.end, {
      allDay: event.allDay,
      timeZone: event.timeZoneId ?? timeZone,
    }),
    title: event.title,
    status: event.statusTypeCode,
    color: event.color,
    info: eventBelongsToSeries(event)
      ? {
          icons: [
            {
              icon: mdiCalendarSync,
              ariaLabel: 'Part of a shift series',
              title: 'Part of a shift series',
            },
          ],
        }
      : undefined,
    actionId: calendarSchedulingActionIds.viewHeaderDetails,
    action: eventHasConflict(event) ? buildPulldownAction() : undefined,
    payload: event,
  };
}

function eventBelongsToSeries(event: CalendarEventBase) {
  if (event.eventSeriesId != null) {
    return true;
  }

  return isCalendarSchedulingEvent(event) && event.metadata.shiftSeriesId != null;
}

function eventHasConflict(event: CalendarEventBase) {
  return 'isConflict' in event && event.isConflict === true;
}

export function getCalendarEventDateKey(
  value: string | Date | undefined | null,
  timeZone?: string,
  locale = 'en-CA',
): string | undefined {
  if (!value) {
    return undefined;
  }

  if (typeof value === 'string') {
    const dateOnly = toCalendarDateOnly(value);

    if (!dateOnly || !/^\d{4}-\d{2}-\d{2}$/.test(dateOnly)) {
      return undefined;
    }

    if (!timeZone || !hasExplicitTimeZoneOffset(value)) {
      return dateOnly;
    }
  }

  const dateTime = toDateTime(value, timeZone);

  if (!dateTime.isValid) {
    return undefined;
  }

  return dateTime.setLocale(locale).toFormat(DATE_FORMAT);
}

function isEventOnMatrixDate(event: CalendarEventBase, date: string, timeZone: string) {
  return getCalendarEventDateKey(event.start, event.timeZoneId ?? timeZone) === date;
}

function resolveMatrixTimeZone(context: CalendarQueryContext) {
  const timeZone = context.filters.timeZoneId ?? context.filters.timeZone;
  return typeof timeZone === 'string' && timeZone.trim() ? timeZone : defaultCalendarSchedulingTimeZone;
}

function buildPulldownAction(): CalendarMatrixActionDisplay {
  return {
    actionId: calendarSchedulingActionIds.showConflict,
    icon: mdiAlertCircle,
    ariaLabel: 'Show conflict details',
    type: CalendarMatrixActionType.Button,
  };
}

function buildDays(startDate: string, period: CalendarPeriod): CalendarMatrixDay[] {
  const firstDate = period === 'day' ? startDate : startOfWeek(startDate);
  const dayCount = resolveDayCount(period);
  const today = formatLocalDateOnly(new Date());

  return Array.from({ length: dayCount }, (_, dayIndex) => {
    const date = addDays(firstDate, dayIndex);

    return {
      date,
      label: formatDayLabel(date),
      isToday: date === today,
    };
  });
}

function resolveDayCount(period: CalendarPeriod) {
  switch (period) {
    case 'day':
      return 1;
    case 'week':
      return 7;
    case 'work-week':
      return 5;
    case 'month':
      return 0;
  }
}

function formatDayLabel(value: string) {
  return new Intl.DateTimeFormat('en-CA', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  }).format(parseLocalDateOnly(value));
}
