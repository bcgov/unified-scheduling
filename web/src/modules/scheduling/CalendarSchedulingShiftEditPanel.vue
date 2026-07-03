<script setup lang="ts">
import { computed } from 'vue';
import type { SelectOption } from '@/types/select';
import CalendarSchedulingShiftForm from './CalendarSchedulingShiftForm.vue';
import type { ShiftResourceFormData } from './calendarSchedulingShiftForm';

const props = defineProps<{
  modelValue: ShiftResourceFormData;
  formErrors: Record<string, string>;
  disabled?: boolean;
  employeeOptions: SelectOption[];
  isLoadingUsers?: boolean;
  showRecurrence: boolean;
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
  <section class="shift-edit-panel" aria-label="Edit shift panel">
    <CalendarSchedulingShiftForm
      v-model="formData"
      id-prefix="edit-shift"
      :form-errors="formErrors"
      :disabled="disabled"
      :show-recurrence="showRecurrence"
      :employee-options="employeeOptions"
      :is-loading-users="isLoadingUsers"
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
