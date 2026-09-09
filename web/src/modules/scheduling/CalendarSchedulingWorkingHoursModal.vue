<script setup lang="ts">
import { computed } from 'vue';
import type { WorkingHoursResult } from '@/api-access/generated/models';
import UaModal from '@/shared/components/UaModal.vue';
import { formatCalendarDateOnly } from '@/utils/date';
import { formatWorkingHoursMinutes } from './calendarSchedulingWorkingHours';

const props = defineProps<{
  result: WorkingHoursResult;
  userName?: string;
}>();

const emit = defineEmits<{
  close: [];
}>();

const regularCreditedMinutes = computed(() => props.result.creditedMinutes - props.result.overtimeMinutes);
const modalTitle = computed(() =>
  [props.userName, formatCalendarDateOnly(props.result.date)].filter(Boolean).join(' · '),
);
</script>

<template>
  <UaModal :title="modalTitle" width="560" @close="emit('close')">
    <table class="working-hours-modal__breakdown">
      <tbody>
        <tr>
          <th scope="row">Paid shift time</th>
          <td>{{ formatWorkingHoursMinutes(result.paidShiftMinutes) }}</td>
        </tr>
        <tr class="working-hours-modal__detail-row">
          <th scope="row">Lunch break</th>
          <td>{{ formatWorkingHoursMinutes(result.lunchAvailableMinutes) }}</td>
        </tr>
        <tr class="working-hours-modal__detail-row">
          <th scope="row">Worked lunch (included in paid shift time)</th>
          <td>{{ formatWorkingHoursMinutes(result.workedLunchMinutes) }}</td>
        </tr>
        <tr>
          <th scope="row">Paid outside-shift assignments</th>
          <td>{{ formatWorkingHoursMinutes(result.paidOutsideShiftMinutes) }}</td>
        </tr>
        <tr>
          <th scope="row">Regular credited time</th>
          <td>{{ formatWorkingHoursMinutes(regularCreditedMinutes) }}</td>
        </tr>
        <tr class="working-hours-modal__total-row">
          <th scope="row">Total credited time</th>
          <td>{{ formatWorkingHoursMinutes(result.creditedMinutes) }}</td>
        </tr>
        <tr class="working-hours-modal__total-row">
          <th scope="row">Overtime</th>
          <td>{{ formatWorkingHoursMinutes(result.overtimeMinutes) }}</td>
        </tr>
      </tbody>
    </table>
  </UaModal>
</template>

<style scoped>
.working-hours-modal__breakdown {
  border-collapse: collapse;
  inline-size: 100%;
}

.working-hours-modal__breakdown th,
.working-hours-modal__breakdown td {
  border-bottom: 1px solid var(--ua-border-color);
  padding: var(--ua-spacing-sm) 0;
}

.working-hours-modal__breakdown th {
  font-weight: var(--ua-font-weight-normal);
  text-align: start;
}

.working-hours-modal__breakdown td {
  font-variant-numeric: tabular-nums;
  font-weight: var(--ua-font-weight-semibold);
  padding-inline-start: var(--ua-spacing-lg);
  text-align: end;
  white-space: nowrap;
}

.working-hours-modal__detail-row {
  color: var(--ua-text-secondary);
}

.working-hours-modal__detail-row th {
  padding-inline-start: var(--ua-spacing-lg);
}

.working-hours-modal__total-row th,
.working-hours-modal__total-row td {
  font-weight: var(--ua-font-weight-bold);
}
</style>
