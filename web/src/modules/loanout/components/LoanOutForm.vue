<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { repeatOptions, type RepeatMode } from '@/modules/scheduling/calendarSchedulingShiftForm';
import type { SelectOption, SelectValue } from '@/types/select';
import { useLocationsStore } from '@/stores/LocationsStore';
import { validateLoanOutRecurrence } from '../loanOutRequest';

export type LoanOutFormData = {
  userIds?: string[];
  loanOutLocationId?: number | string | null;
  date?: string;
  startTime?: string;
  endTime?: string;
  repeatMode?: RepeatMode;
  recurrenceRule?: string | null;
  notes?: string | null;
};

const props = defineProps<{
  modelValue: LoanOutFormData;
  homeLocationId?: number | null;
  disabled?: boolean;
  showEmployees?: boolean;
  showRepeat?: boolean;
  timeZoneId?: string;
  employeeOptions?: SelectOption[];
  loadingEmployees?: boolean;
  formErrors?: Record<string, string>;
}>();
const emit = defineEmits<{
  (event: 'update:modelValue', value: LoanOutFormData): void;
  (event: 'validation-change', hasError: boolean): void;
}>();
const locationsStore = useLocationsStore();
const destinationOptions = computed(() =>
  locationsStore.selectOptions.filter(
    (option) => props.homeLocationId == null || String(option.code) !== String(props.homeLocationId),
  ),
);
const isLoadingLocations = ref(false);
const locationsError = ref('');
const editorError = ref('');
const formErrors = computed(() => props.formErrors ?? {});
const recurrenceError = computed(() =>
  props.modelValue.repeatMode === 'custom'
    ? editorError.value || validateLoanOutRecurrence(props.modelValue.recurrenceRule, props.modelValue.date)
    : '',
);
watch(
  () => [props.modelValue.repeatMode, props.modelValue.recurrenceRule, props.modelValue.date] as const,
  ([repeatMode, rule, date]) => {
    if (repeatMode === 'custom' && rule) {
      editorError.value = validateLoanOutRecurrence(rule, date);
    } else {
      editorError.value = '';
    }
  },
  { immediate: true },
);
watch(recurrenceError, (error) => emit('validation-change', Boolean(error)), { immediate: true });
watch(
  () => props.modelValue.repeatMode,
  () => {
    editorError.value = '';
  },
);

onMounted(async () => {
  if (locationsStore.selectOptions.length) return;
  isLoadingLocations.value = true;
  try {
    await locationsStore.getEntities();
  } catch (error: unknown) {
    locationsError.value = error instanceof Error ? error.message : 'Failed to load loan-out locations.';
  } finally {
    isLoadingLocations.value = false;
  }
});

function updateField<K extends keyof LoanOutFormData>(field: K, value: LoanOutFormData[K]) {
  emit('update:modelValue', { ...props.modelValue, [field]: value });
}
function updateRepeatMode(value: SelectValue | undefined) {
  const repeatMode = value === 'custom' ? 'custom' : 'never';
  editorError.value = '';
  emit('update:modelValue', {
    ...props.modelValue,
    repeatMode,
    ...(repeatMode === 'never' ? { recurrenceRule: null } : {}),
  });
}
function updateRecurrenceRule(value: string | null) {
  editorError.value = '';
  updateField('recurrenceRule', value);
}
function updateEmployees(value: SelectValue | undefined) {
  updateField('userIds', Array.isArray(value) ? value.filter((id): id is string => typeof id === 'string') : []);
}
</script>

<template>
  <UaAlert v-if="locationsError" type="error" :closable="false">{{ locationsError }}</UaAlert>
  <UaFormGrid>
    <label v-if="showEmployees" class="loan-out__label" for="loan-out-employees">Employee(s)</label>
    <div v-if="showEmployees">
      <UaSelect
        id="loan-out-employees"
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
      <p v-if="formErrors.userIds" class="loan-out__error" role="alert">{{ formErrors.userIds }}</p>
    </div>

    <label class="loan-out__label" for="loan-out-location">Loan-out Location</label>
    <div>
      <UaSelect
        id="loan-out-location"
        :model-value="modelValue.loanOutLocationId"
        :items="destinationOptions"
        :loading="isLoadingLocations"
        :disabled="disabled || isLoadingLocations"
        :error="Boolean(formErrors.loanOutLocationId)"
        placeholder="Select a loan-out location"
        aria-label="Loan-out Location"
        @update:model-value="
          (value: SelectValue | undefined) =>
            updateField('loanOutLocationId', typeof value === 'string' || typeof value === 'number' ? value : null)
        "
      />
      <p v-if="formErrors.loanOutLocationId" class="loan-out__error" role="alert">{{ formErrors.loanOutLocationId }}</p>
    </div>

    <label class="loan-out__label" for="loan-out-date">Date</label>
    <UaTextField
      id="loan-out-date"
      label=""
      type="date"
      :model-value="modelValue.date"
      :error-messages="formErrors.date"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('date', value)"
    />

    <label class="loan-out__label" for="loan-out-all-day">All day</label>
    <v-checkbox
      id="loan-out-all-day"
      :model-value="true"
      aria-label="All day"
      disabled
      hide-details
      density="compact"
    />

    <template v-if="showRepeat ?? true">
      <label class="loan-out__label" for="loan-out-repeat">Repeat</label>
      <UaSelect
        id="loan-out-repeat"
        :model-value="modelValue.repeatMode ?? 'never'"
        aria-label="Repeat"
        :items="repeatOptions"
        :disabled="disabled"
        @update:model-value="updateRepeatMode"
      />
      <RRuleEditor
        v-if="modelValue.repeatMode === 'custom'"
        id-prefix="loan-out-recurrence"
        :model-value="modelValue.recurrenceRule ?? null"
        :start-date="modelValue.date ?? null"
        :disabled="disabled"
        use-parent-grid
        @update:model-value="updateRecurrenceRule"
        @invalid="editorError = $event"
      />
      <template v-else>
        <span aria-hidden="true"></span>
        <p class="loan-out__helper">This loan-out will not repeat.</p>
      </template>
      <template v-if="recurrenceError || formErrors.recurrenceRule">
        <span aria-hidden="true"></span>
        <p class="loan-out__error" role="alert">{{ recurrenceError || formErrors.recurrenceRule }}</p>
      </template>
    </template>

    <UaTextarea
      id="loan-out-notes"
      :model-value="modelValue.notes ?? ''"
      label="Notes"
      :error-messages="formErrors.notes"
      :disabled="disabled"
      @update:model-value="(value: string) => updateField('notes', value)"
    />
  </UaFormGrid>
</template>

<style scoped>
.loan-out__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}
.loan-out__caption {
  color: var(--ua-text-secondary);
  display: block;
  font-size: var(--ua-font-size-sm);
}
.loan-out__error {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin: var(--ua-spacing-xs) 0 0;
}
.loan-out__helper {
  color: var(--ua-text-secondary);
  margin: 0;
}
</style>
