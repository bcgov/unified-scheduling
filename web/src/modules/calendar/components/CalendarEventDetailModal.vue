<script setup lang="ts">
import { computed } from 'vue';
import UaDisplayField from '@/shared/components/UaDisplayField.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { addDays, formatCalendarDateOnly, formatCalendarDateTimeDate, formatCalendarTime } from '../calendarDateUtils';
import type { CalendarEventBase } from '../calendarTypes';

const props = defineProps<{
  event: CalendarEventBase;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const locationsStore = useLocationsStore();

const eventTimeZone = computed(() => props.event.timeZoneId || 'UTC');

const displayedEnd = computed(() => {
  if (!props.event.end) {
    return props.event.start;
  }

  return props.event.allDay ? addDays(props.event.end, -1) : props.event.end;
});

const locationName = computed(() => {
  if (props.event.locationId == null) {
    return 'Unassigned';
  }

  return locationsStore.entitiesMap[String(props.event.locationId)]?.name || 'Unknown location';
});

const description = computed(() => props.event.description?.trim() || '—');
const notes = computed(() => props.event.notes?.trim() || '—');
const startDate = computed(() => formatEventDate(props.event.start));
const endDate = computed(() => formatEventDate(displayedEnd.value));
const timeDisplay = computed(() => {
  if (props.event.allDay) {
    return 'All day';
  }

  const start = formatTime(props.event.start);
  const end = props.event.end ? formatTime(props.event.end) : undefined;

  return end ? `${start} - ${end}` : start;
});

function formatEventDate(value: string) {
  if (props.event.allDay) {
    return formatCalendarDateOnly(value);
  }

  return formatCalendarDateTimeDate(value, eventTimeZone.value);
}

function formatTime(value: string | Date) {
  return formatCalendarTime(value, eventTimeZone.value);
}
</script>

<template>
  <UaModal title="Event Details" @close="emit('close')">
    <UaFormGrid label-width="140px">
      <UaDisplayField label="Title" :value="event.title" />
      <UaDisplayField label="Description" :value="description" />
      <UaDisplayField label="Notes" :value="notes" />
      <UaDisplayField label="Start date" :value="startDate" />
      <UaDisplayField label="End date" :value="endDate" />
      <UaDisplayField label="Time" :value="timeDisplay" />
      <UaDisplayField label="Location" :value="locationName" />
    </UaFormGrid>
  </UaModal>
</template>
