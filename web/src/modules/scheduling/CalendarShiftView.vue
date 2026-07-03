<script setup lang="ts">
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import CalendarSchedulingView from './CalendarSchedulingView.vue';

defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();
</script>

<template>
  <CalendarSchedulingView :model="model" :runtime-context="runtimeContext" @event-click="emit('eventClick', $event)" />
</template>
