<script setup lang="ts">
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import type { SelectOption, SelectValue } from '@/types/select';
import { computed } from 'vue';
import {
  cancelOptions,
  publishOptions,
  repeatOptions,
  timeOptions,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';

const props = withDefaults(
  defineProps<{
    modelValue: ShiftResourceFormData;
    formErrors?: Record<string, string>;
    disabled?: boolean;
    showRecurrence?: boolean;
    employeeOptions: SelectOption[];
    isLoadingUsers?: boolean;
    idPrefix?: string;
  }>(),
  {
    formErrors: () => ({}),
    disabled: false,
    showRecurrence: true,
    isLoadingUsers: false,
    idPrefix: 'shift-form',
  },
);

const emit = defineEmits<{
  'update:modelValue': [value: ShiftResourceFormData];
  recurrenceChange: [value: string | null];
  recurrenceInvalid: [reason: string];
}>();

const formData = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value),
});

const statusTypeCode = computed(() => String(formData.value.statusTypeCode ?? '').toLowerCase());
const isDraftStatus = computed(() => statusTypeCode.value === 'draft');
const isActiveStatus = computed(() => statusTypeCode.value === 'active');

function updateField<TKey extends keyof ShiftResourceFormData>(key: TKey, value: ShiftResourceFormData[TKey]) {
  formData.value = {
    ...formData.value,
    [key]: value,
  };
}

function updateSelectField<TKey extends keyof ShiftResourceFormData>(key: TKey, value: SelectValue | undefined) {
  updateField(key, (value ?? null) as ShiftResourceFormData[TKey]);
}

function handleRecurrenceChange(value: string | null) {
  updateField('recurrenceRule', value);
  emit('recurrenceChange', value);
}
</script>

<template>
  <UaFormGrid label-width="110px">
    <UaTextField
      :id="`${idPrefix}-date`"
      label="Date"
      type="date"
      :model-value="formData.date"
      :error-messages="formErrors.date"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('date', value)"
    />

    <span :id="`${idPrefix}-time-label`" class="shift-form__label">Time</span>
    <div class="shift-form__time-fields" :aria-labelledby="`${idPrefix}-time-label`">
      <div class="shift-form__time-field">
        <span class="shift-form__time-caption">Start</span>
        <UaSelect
          :model-value="formData.startTime"
          aria-label="Start time"
          :items="timeOptions"
          :error="Boolean(formErrors.startTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('startTime', value)"
        />
        <p v-if="formErrors.startTime" class="shift-form__field-error">
          {{ formErrors.startTime }}
        </p>
      </div>
      <div class="shift-form__time-field">
        <span class="shift-form__time-caption">End</span>
        <UaSelect
          :model-value="formData.endTime"
          aria-label="End time"
          :items="timeOptions"
          :error="Boolean(formErrors.endTime)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('endTime', value)"
        />
        <p v-if="formErrors.endTime" class="shift-form__field-error">
          {{ formErrors.endTime }}
        </p>
      </div>
    </div>

    <template v-if="props.showRecurrence">
      <label class="shift-form__label" :for="`${idPrefix}-repeat`">Repeat</label>

      <div class="shift-form__repeat-field">
        <UaSelect
          :id="`${idPrefix}-repeat`"
          :model-value="formData.repeatMode"
          aria-label="Repeat"
          :items="repeatOptions"
          :error="Boolean(formErrors.repeatMode)"
          :disabled="disabled"
          @update:model-value="(value: SelectValue | undefined) => updateSelectField('repeatMode', value)"
        />
        <p v-if="formErrors.repeatMode" class="shift-form__field-error">
          {{ formErrors.repeatMode }}
        </p>
      </div>

      <RRuleEditor
        v-if="formData.repeatMode === 'custom'"
        :id-prefix="`${idPrefix}-recurrence`"
        :model-value="formData.recurrenceRule ?? null"
        :start-date="formData.date ?? null"
        :disabled="disabled"
        use-parent-grid
        @update:model-value="handleRecurrenceChange"
        @change="handleRecurrenceChange"
        @invalid="(reason: string) => emit('recurrenceInvalid', reason)"
      />
      <template v-else>
        <span aria-hidden="true"></span>
        <p class="shift-form__helper-text">This shift will not repeat.</p>
      </template>

      <template v-if="formErrors.recurrenceRule">
        <span aria-hidden="true"></span>
        <p class="shift-form__field-error">
          {{ formErrors.recurrenceRule }}
        </p>
      </template>
    </template>

    <label class="shift-form__label" :for="`${idPrefix}-employee`">Employee</label>
    <UaSelect
      :id="`${idPrefix}-employee`"
      :model-value="formData.userIds"
      aria-label="Employee"
      :items="employeeOptions"
      :disabled="disabled || isLoadingUsers"
      :loading="isLoadingUsers"
      multiple
      chips
      closable-chips
      clearable
      @update:model-value="(value: SelectValue | undefined) => updateSelectField('userIds', value)"
    />

    <UaTextField
      :id="`${idPrefix}-assignment`"
      label="Assignment"
      :model-value="formData.assignmentLabel"
      placeholder=""
      :disabled="true"
      @update:model-value="(value: string) => updateField('assignmentLabel', value)"
    />

    <UaTextField
      :id="`${idPrefix}-training`"
      label="Training"
      :model-value="formData.trainingLabel"
      placeholder=""
      :disabled="true"
      @update:model-value="(value: string) => updateField('trainingLabel', value)"
    />

    <label v-if="isDraftStatus" class="shift-form__label" :for="`${idPrefix}-publish`">Publish</label>
    <div v-if="isDraftStatus" class="shift-form__status-field">
      <UaSelect
        :id="`${idPrefix}-publish`"
        :model-value="formData.publish"
        aria-label="Publish"
        :items="publishOptions"
        :error="Boolean(formErrors.publish)"
        :disabled="disabled"
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('publish', value)"
      />
      <p v-if="formErrors.publish" class="shift-form__field-error">
        {{ formErrors.publish }}
      </p>
    </div>

    <label v-if="isActiveStatus" class="shift-form__label" :for="`${idPrefix}-cancel`">Cancel</label>
    <div v-if="isActiveStatus" class="shift-form__status-field">
      <UaSelect
        :id="`${idPrefix}-cancel`"
        :model-value="formData.cancel"
        aria-label="Cancel"
        :items="cancelOptions"
        :error="Boolean(formErrors.cancel)"
        :disabled="disabled"
        @update:model-value="(value: SelectValue | undefined) => updateSelectField('cancel', value)"
      />
      <p v-if="formErrors.cancel" class="shift-form__field-error">
        {{ formErrors.cancel }}
      </p>
    </div>

    <UaTextarea
      :id="`${idPrefix}-notes`"
      label="Notes"
      :model-value="formData.notes ?? ''"
      :disabled="disabled"
      :error-messages="formErrors.notes"
      rows="3"
      counter="200"
      @update:model-value="(value: string) => updateField('notes', value)"
    />
  </UaFormGrid>
</template>

<style scoped>
.shift-form__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.shift-form__time-fields {
  display: grid;
  gap: var(--ua-spacing-md);
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.shift-form__time-field,
.shift-form__repeat-field,
.shift-form__status-field {
  display: grid;
  gap: var(--ua-spacing-xs);
}

.shift-form__repeat-field {
  gap: var(--ua-spacing-md);
}

.shift-form__time-caption {
  color: var(--ua-text-secondary);
  display: block;
  font-size: var(--ua-font-size-sm);
}

.shift-form__helper-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.shift-form__field-error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}

@media (max-width: 640px) {
  .shift-form__time-fields {
    grid-template-columns: 1fr;
  }
}
</style>
