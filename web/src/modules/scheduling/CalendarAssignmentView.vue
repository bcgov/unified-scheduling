<script setup lang="ts">
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import CalendarSchedulingView from './CalendarSchedulingView.vue';

defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
  renderToken?: string;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
  (event: 'renderComplete', renderToken?: string): void;
}>();
</script>

<template>
  <CalendarSchedulingView
    :model="model"
    :render-token="renderToken"
    :runtime-context="runtimeContext"
    show-assignment-content
    @event-click="emit('eventClick', $event)"
    @render-complete="emit('renderComplete', $event)"
  />
</template>
