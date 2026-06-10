<script setup lang="ts">
import type { EventClickArg, EventInput } from '@fullcalendar/core';
import { computed } from 'vue';
import UaCalendar from '@/shared/components/UaCalendar.vue';
import type { CalendarEventBase, CalendarFullCalendarViewModel } from '../calendarTypes';

const props = defineProps<{
  model: CalendarFullCalendarViewModel;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();

const events = computed<EventInput[]>(() =>
  props.model.events.map((event) => ({
    id: event.id,
    title: event.title,
    start: event.start,
    end: event.end,
    allDay: event.allDay,
    extendedProps: {
      calendarEvent: event,
      type: event.type,
      sourceModule: event.sourceModule,
      description: event.description,
      eventTypeCode: event.eventTypeCode,
      status: event.status,
    },
  })),
);

const handleEventClick = (arg: EventClickArg) => {
  const calendarEvent = arg.event.extendedProps['calendarEvent'];

  if (calendarEvent) {
    emit('eventClick', calendarEvent as CalendarEventBase);
  }
};
</script>

<template>
  <UaCalendar
    :events="events"
    :view="model.view"
    :initial-date="model.initialDate"
    :weekends="model.weekends"
    @event-click="handleEventClick"
  />
</template>
