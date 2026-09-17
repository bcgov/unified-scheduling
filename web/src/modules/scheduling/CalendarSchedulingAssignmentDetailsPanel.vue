<script setup lang="ts">
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
  isLoading?: boolean;
}>();
</script>

<template>
  <CalendarSchedulingShiftDetailsPanel
    aria-label="Assignment Details Panel"
    :detail-rows="detailRows"
    :is-loading="isLoading"
  />
</template>
