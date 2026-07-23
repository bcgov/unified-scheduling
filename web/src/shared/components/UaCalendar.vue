<script setup lang="ts">
import FullCalendar, {
  type CalendarOptions,
  type EventClickInfo,
  type EventDisplayInfo,
  type EventInput,
} from '@fullcalendar/vue3';
import classicThemePlugin from '@fullcalendar/vue3/themes/classic';
import dayGridPlugin from '@fullcalendar/vue3/daygrid';
import interactionPlugin from '@fullcalendar/vue3/interaction';
import listPlugin from '@fullcalendar/vue3/list';
import timeGridPlugin from '@fullcalendar/vue3/timegrid';
import { computed } from 'vue';

const emit = defineEmits<{
  (event: 'eventClick', payload: EventClickInfo): void;
}>();

type UaCalendarView = 'timeGridDay' | 'timeGridWeek' | 'dayGridMonth' | 'listWeek';

type UaFullCalendarProps = {
  options?: Partial<CalendarOptions>;
  [key: string]: unknown;
};

function toCalendarEventClassToken(value: string): string {
  let normalized = value
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, '-')
    .replace(/^-+/, '');

  while (normalized.endsWith('-')) {
    normalized = normalized.slice(0, -1);
  }

  return normalized || 'unknown';
}

function normalizeClassNameInput(value: string | undefined | null | false | 0): string[] {
  if (typeof value !== 'string') {
    return [];
  }

  return value.split(/\s+/).filter((item) => item.length > 0);
}

function resolveEventClassNames(value: CalendarOptions['eventClass'], arg: EventDisplayInfo): string[] {
  if (!value) {
    return [];
  }

  if (typeof value === 'function') {
    return normalizeClassNameInput(value(arg));
  }

  return normalizeClassNameInput(value);
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
  plugins: [classicThemePlugin, dayGridPlugin, timeGridPlugin, listPlugin, interactionPlugin],
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
  slotHeaderInterval: '03:00:00',
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
  listDayAltFormat: false,
  eventClick(arg) {
    emit('eventClick', arg);
  },
  eventClass(arg) {
    const eventTypeCode = toCalendarEventClassToken(String(arg.event.extendedProps['eventTypeCode'] ?? 'general'));
    const eventType = toCalendarEventClassToken(String(arg.event.extendedProps['type'] ?? 'calendar.general'));
    return `ua-calendar__event--${eventTypeCode} ua-calendar__event-type--${eventType}`;
  },
}));

const mergedCalendarOptions = computed<CalendarOptions>(() => {
  const optionOverrides = props.fullCalendarProps?.options;
  const defaultOptions = defaultCalendarOptions.value;
  const defaultEventClick = defaultOptions.eventClick;
  const overrideEventClick = optionOverrides?.eventClick;
  const defaultEventClass = defaultOptions.eventClass;
  const overrideEventClass = optionOverrides?.eventClass;

  return {
    ...defaultOptions,
    ...optionOverrides,
    plugins: optionOverrides?.plugins ?? defaultOptions.plugins,
    eventClick(arg) {
      defaultEventClick?.(arg);
      overrideEventClick?.(arg);
    },
    eventClass(arg) {
      const defaultClassNames = resolveEventClassNames(defaultEventClass, arg);
      const overrideClassNames = resolveEventClassNames(overrideEventClass, arg);

      const mergedClassNames = [...defaultClassNames, ...overrideClassNames];
      return mergedClassNames.length > 0 ? mergedClassNames.join(' ') : undefined;
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
