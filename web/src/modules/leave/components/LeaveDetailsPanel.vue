<script setup lang="ts">
import CalendarSchedulingShiftDetailsPanel from '@/modules/scheduling/CalendarSchedulingShiftDetailsPanel.vue';
import type { AuditFields } from '@/shared/components/UaAuditDisplay.vue';
import type { ShiftDetailRow } from '@/modules/scheduling/calendarSchedulingShiftDetailTypes';

defineProps<{
  detailRows: ShiftDetailRow[];
  isLoading?: boolean;
  audit?: AuditFields | null;
  timeZone?: string;
}>();
</script>

<template>
  <CalendarSchedulingShiftDetailsPanel
    aria-label="Leave Details Panel"
    :detail-rows="detailRows"
    :is-loading="isLoading"
    :audit="audit"
    :time-zone="timeZone"
  />
</template>
