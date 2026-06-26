<script setup lang="ts">
import FullCalendar from '@fullcalendar/vue3';
import dayGridPlugin from '@fullcalendar/daygrid';
import interactionPlugin from '@fullcalendar/interaction';
import listPlugin from '@fullcalendar/list';
import timeGridPlugin from '@fullcalendar/timegrid';
import type { CalendarOptions, EventClickArg, EventContentArg, EventInput } from '@fullcalendar/core';
import { computed } from 'vue';

const emit = defineEmits<{
  (event: 'eventClick', payload: EventClickArg): void;
}>();

type UaCalendarView = 'timeGridDay' | 'timeGridWeek' | 'dayGridMonth' | 'listWeek';

type UaFullCalendarProps = {
  options?: Partial<CalendarOptions>;
  [key: string]: unknown;
};

function toCalendarEventClassToken(value: string): string {
  return (
    value
      .toLowerCase()
      .replace(/[^a-z0-9_-]+/g, '-')
      .replace(/^-+|-+$/g, '') || 'unknown'
  );
}

function resolveEventClassNames(value: CalendarOptions['eventClassNames'], arg: EventContentArg): string[] {
  if (!value) {
    return [];
  }

  if (typeof value === 'function') {
    const result = value(arg);
    return (Array.isArray(result) ? result : [result]).filter(
      (item): item is string => typeof item === 'string' && item.length > 0,
    );
  }

  return Array.isArray(value) ? value : [value];
}

const props = withDefaults(
  defineProps<{
    events: EventInput[];
    view: UaCalendarView;
    initialDate?: string;
    height?: string | number;
    weekends?: boolean;
    fullCalendarProps?: UaFullCalendarProps;
  }>(),
  {
    initialDate: undefined,
    height: 'auto',
    weekends: true,
    fullCalendarProps: undefined,
  },
);

const calendarKey = computed(
  () => `${props.view}:${props.initialDate ?? ''}:${props.weekends ? 'weekends' : 'weekdays'}`,
);

const defaultCalendarOptions = computed<CalendarOptions>(() => ({
  plugins: [dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin],
  initialView: props.view,
  initialDate: props.initialDate,
  events: props.events,
  height: props.height,
  weekends: props.weekends,
  headerToolbar: false,
  dayHeaderFormat:
    props.view === 'dayGridMonth'
      ? {
          weekday: 'short',
        }
      : {
          weekday: 'short',
          month: 'short',
          day: 'numeric',
        },
  allDaySlot: true,
  nowIndicator: true,
  displayEventEnd: true,
  firstDay: 1,
  slotMinTime: '06:00:00',
  slotMaxTime: '20:00:00',
  slotDuration: '03:00:00',
  slotLabelInterval: '03:00:00',
  eventTimeFormat: {
    hour: 'numeric',
    minute: '2-digit',
    meridiem: 'short',
  },
  listDayFormat: {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  },
  listDaySideFormat: false,
  eventClick(arg) {
    emit('eventClick', arg);
  },
  eventClassNames(arg) {
    const eventTypeCode = toCalendarEventClassToken(String(arg.event.extendedProps['eventTypeCode'] ?? 'general'));
    const eventType = toCalendarEventClassToken(String(arg.event.extendedProps['type'] ?? 'calendar.general'));
    return [`ua-calendar__event--${eventTypeCode}`, `ua-calendar__event-type--${eventType}`];
  },
}));

const mergedCalendarOptions = computed<CalendarOptions>(() => {
  const optionOverrides = props.fullCalendarProps?.options;
  const defaultOptions = defaultCalendarOptions.value;
  const defaultEventClick = defaultOptions.eventClick;
  const overrideEventClick = optionOverrides?.eventClick;
  const defaultEventClassNames = defaultOptions.eventClassNames;
  const overrideEventClassNames = optionOverrides?.eventClassNames;

  return {
    ...defaultOptions,
    ...optionOverrides,
    plugins: optionOverrides?.plugins ?? defaultOptions.plugins,
    eventClick(arg) {
      defaultEventClick?.(arg);
      overrideEventClick?.(arg);
    },
    eventClassNames(arg) {
      const defaultClassNames = resolveEventClassNames(defaultEventClassNames, arg);
      const overrideClassNames = resolveEventClassNames(overrideEventClassNames, arg);

      return [...defaultClassNames, ...overrideClassNames];
    },
  };
});

const forwardedFullCalendarProps = computed(() => {
  if (!props.fullCalendarProps) {
    return { options: mergedCalendarOptions.value };
  }

  const propOverrides = { ...props.fullCalendarProps };
  delete propOverrides.options;

  return {
    ...propOverrides,
    options: mergedCalendarOptions.value,
  };
});
</script>

<template>
  <div class="ua-calendar">
    <FullCalendar :key="calendarKey" v-bind="forwardedFullCalendarProps" />
  </div>
</template>
