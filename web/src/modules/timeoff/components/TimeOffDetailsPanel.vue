<script setup lang="ts">
import CalendarSchedulingShiftDetailsPanel from '@/modules/scheduling/CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from '@/modules/scheduling/calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
  isLoading?: boolean;
}>();
</script>

<template>
  <CalendarSchedulingShiftDetailsPanel
    aria-label="Time Off Details Panel"
    :detail-rows="detailRows"
    :is-loading="isLoading"
  />
</template>
