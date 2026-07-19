<script setup lang="ts">
import { computed } from 'vue';
import type { SelectOption } from '@/types/select';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';

const props = defineProps<{
  modelValue: ShiftResourceFormData;
  formErrors: Record<string, string>;
  disabled?: boolean;
  locationOptions: SelectOption[];
  employeeOptions: SelectOption[];
  isLoadingUsers?: boolean;
  assignmentEntryOptions?: SelectOption[];
  assignmentSeriesOptions?: SelectOption[];
  assignmentWarning?: string;
  isLoadingAssignments?: boolean;
  showRecurrence: boolean;
  showSeriesAssignment?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: ShiftResourceFormData];
  recurrenceChange: [value: string | null];
  recurrenceInvalid: [reason: string];
}>();

const formData = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
});
</script>

<template>
  <section class="shift-edit-panel" aria-label="Edit Shift Panel">
    <CalendarSchedulingShiftForm
      v-model="formData"
      id-prefix="edit-shift"
      :form-errors="formErrors"
      :disabled="disabled"
      :show-recurrence="showRecurrence"
      :location-options="locationOptions"
      :employee-options="employeeOptions"
      :is-loading-users="isLoadingUsers"
      :assignment-entry-options="assignmentEntryOptions ?? []"
      :assignment-series-options="assignmentSeriesOptions ?? []"
      :assignment-warning="assignmentWarning ?? ''"
      :is-loading-assignments="isLoadingAssignments"
      :show-series-assignment="showSeriesAssignment"
      @recurrence-change="(value: string | null) => emit('recurrenceChange', value)"
      @recurrence-invalid="(reason: string) => emit('recurrenceInvalid', reason)"
    />
  </section>
</template>

<style scoped>
.shift-edit-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}
</style>
