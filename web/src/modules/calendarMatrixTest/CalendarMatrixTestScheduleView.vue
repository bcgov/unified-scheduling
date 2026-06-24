<script setup lang="ts">
import CalendarMatrixTestView from './CalendarMatrixTestView.vue';
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';

defineProps<{
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
}>();

const emit = defineEmits<{
  (event: 'eventClick', payload: CalendarEventBase): void;
}>();
</script>

<template>
  <CalendarMatrixTestView :model="model" :runtime-context="runtimeContext" @event-click="emit('eventClick', $event)" />
</template>
