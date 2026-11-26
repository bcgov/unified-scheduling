<script setup lang="ts">
import type { AuditFields } from '@/shared/components/UaAuditDisplay.vue';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
  isLoading?: boolean;
  audit?: AuditFields | null;
  timeZone?: string;
}>();
</script>

<template>
  <CalendarSchedulingShiftDetailsPanel
    aria-label="Assignment Details Panel"
    :detail-rows="detailRows"
    :is-loading="isLoading"
    :audit="audit"
    :time-zone="timeZone"
  />
</template>
