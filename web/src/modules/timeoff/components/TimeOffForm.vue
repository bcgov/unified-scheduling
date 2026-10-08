<script setup lang="ts">
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { repeatOptions, type RepeatMode } from '@/modules/scheduling/calendarSchedulingShiftForm';
import { defaultEndTime, defaultStartTime, timeOptions } from '@/modules/scheduling/schedulingDateTime';
import type { SelectOption, SelectValue } from '@/types/select';
import { ref } from 'vue';
import { useLeaveTypeOptions } from '../composables/useLeaveTypeOptions';

export type TimeOffFormData = {
  userIds?: string[];
  leaveType?: string | number | null;
  date?: string;
  startTime?: string;
  endTime?: string;
  repeatMode?: RepeatMode;
  recurrenceRule?: string | null;
  notes?: string | null;
};

export type EmployeeProps = {
  employeeOptions?: SelectOption[];
  loadingEmployees?: boolean;
};

const props = defineProps<
  {
    modelValue: TimeOffFormData;
    disabled?: boolean;
    showEmployees?: boolean;
  } & EmployeeProps
>();

const emit = defineEmits<{
  (event: 'update:modelValue', value: TimeOffFormData): void;
}>();

const { leaveTypeOptions, isLoadingLeaveTypes, leaveTypesError } = useLeaveTypeOptions();
const formErrors = ref<Record<string, string>>({});

const updateField = (field: keyof TimeOffFormData, value: string | string[] | number | null | undefined) => {
  emit('update:modelValue', { ...props.modelValue, [field]: value });
};

const updateSelectField = (field: 'startTime' | 'endTime', value: SelectValue | undefined) => {
  updateField(field, typeof value === 'string' ? value : undefined);
};

const updateRepeatMode = (value: SelectValue | undefined) => {
  const repeatMode = value === 'custom' ? 'custom' : 'never';
  emit('update:modelValue', {
    ...props.modelValue,
    repeatMode,
    ...(repeatMode === 'never' ? { recurrenceRule: null } : {}),
  });
};

const updateRecurrenceRule = (value: string | null) => {
  emit('update:modelValue', { ...props.modelValue, recurrenceRule: value });
};

const updateEmployees = (value: SelectValue | undefined) => {
  updateField('userIds', Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []);
};
</script>

<template>
  <UaFormGrid>
    <label v-if="showEmployees" class="user-timeoff__label" for="user-timeoff-employee">Employee(s)</label>
    <div v-if="showEmployees" class="user-timeoff__field">
      <UaSelect
        id="user-timeoff-employee"
        :model-value="modelValue.userIds"
        aria-label="Employee(s)"
        :items="employeeOptions ?? []"
        :error="Boolean(formErrors.userIds)"
        :disabled="disabled || loadingEmployees"
        :loading="loadingEmployees"
        multiple
        chips
        closable-chips
        clearable
        @update:model-value="updateEmployees"
      />
      <p v-if="formErrors.userIds" class="user-timeoff__field-error">{{ formErrors.userIds }}</p>
    </div>

    <label class="user-timeoff__label" for="user-timeoff-type">Leave Type</label>
    <div class="user-timeoff__field">
      <UaSelect
        id="user-timeoff-type"
        :model-value="modelValue.leaveType"
        :items="leaveTypeOptions"
        :loading="isLoadingLeaveTypes"
        :error="Boolean(leaveTypesError)"
        :disabled="disabled || isLoadingLeaveTypes"
        placeholder="Select a leave type"
        aria-label="Leave Type"
        @update:model-value="
          (value: SelectValue | undefined) =>
            updateField('leaveType', typeof value === 'string' || typeof value === 'number' ? value : null)
        "
      />
      <p v-if="leaveTypesError" class="user-timeoff__field-error">{{ leaveTypesError }}</p>
    </div>

    <label class="user-timeoff__label" for="user-timeoff-date">Date</label>
    <UaTextField
      id="user-timeoff-date"
      label=""
      type="date"
      :model-value="modelValue.date"
      :error-messages="formErrors.date"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('date', value)"
    />

    <label class="user-timeoff__label" for="user-timeoff-repeat">Repeat</label>
    <UaSelect
      id="user-timeoff-repeat"
      :model-value="modelValue.repeatMode ?? 'never'"
      aria-label="Repeat"
      :items="repeatOptions"
      :disabled="disabled"
      @update:model-value="updateRepeatMode"
    />

    <RRuleEditor
      v-if="(modelValue.repeatMode ?? 'never') === 'custom'"
      id-prefix="user-timeoff-recurrence"
      :model-value="modelValue.recurrenceRule ?? null"
      :start-date="modelValue.date ?? null"
      :disabled="disabled"
      use-parent-grid
      @update:model-value="updateRecurrenceRule"
    />
    <template v-else>
      <span aria-hidden="true"></span>
      <p class="user-timeoff__helper-text">This leave will not repeat.</p>
    </template>

    <span id="user-timeoff-time-label" class="user-timeoff__label">Time</span>
    <div class="user-timeoff__time-fields" aria-labelledby="user-timeoff-time-label">
      <div class="user-timeoff__field">
        <span class="user-timeoff__time-caption">Start</span>
        <UaSelect
          :model-value="modelValue.startTime ?? defaultStartTime"
          aria-label="Start Time"
          :items="timeOptions"
          :error="Boolean(formErrors.startTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('startTime', value)"
        />
        <p v-if="formErrors.startTime" class="user-timeoff__field-error">{{ formErrors.startTime }}</p>
      </div>
      <div class="user-timeoff__field">
        <span class="user-timeoff__time-caption">End</span>
        <UaSelect
          :model-value="modelValue.endTime ?? defaultEndTime"
          aria-label="End Time"
          :items="timeOptions"
          :error="Boolean(formErrors.endTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('endTime', value)"
        />
        <p v-if="formErrors.endTime" class="user-timeoff__field-error">{{ formErrors.endTime }}</p>
      </div>
    </div>

    <UaTextarea
      id="user-timeoff-notes"
      :model-value="modelValue.notes ?? ''"
      label="Notes"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('notes', value)"
    />
  </UaFormGrid>
</template>

<style scoped>
.user-timeoff__time-fields {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.user-timeoff__field {
  min-width: 0;
}

.user-timeoff__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.user-timeoff__time-caption {
  color: var(--ua-text-secondary);
  display: block;
  font-size: var(--ua-font-size-sm);
}

.user-timeoff__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

.user-timeoff__helper-text {
  color: var(--ua-text-secondary);
  margin: 0;
}

@media (max-width: 640px) {
  .user-timeoff__time-fields {
    grid-template-columns: 1fr;
  }
}
</style>
