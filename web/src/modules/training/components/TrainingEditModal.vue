<script setup lang="ts">
import { putApiLookupTrainingsId } from '@/api-access/generated/training/training';
import type { TrainingLookupRequest, TrainingLookupResponse } from '@/api-access/generated/models';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import type { SelectOption } from '@/types/select';
import { mdiClose, mdiContentSave } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import { useTrainingProfiles } from '../trainingProfileApi';
import {
  annualValidityDayCode,
  getValidityDayCodeFromDays,
  getValidityDayOptions,
  getValidityDaysFromCode,
} from '../validityDayOptions';

const props = defineProps<{
  training: TrainingLookupResponse;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'updated', training: TrainingLookupResponse | null): void;
}>();

type TrainingFormData = {
  code: string;
  description?: string | null;
  mandatory?: boolean;
  mandatoryTrainingProfileIds: number[];
  rotating?: boolean;
  validityDayCode: string;
  advanceNoticeDays: string;
  trainingCategoryId?: number | null;
};

const isLoading = ref(false);
const apiErrorMessage = ref('');
const formErrors = ref<Record<string, string>>({});

const { data: trainingProfiles, isFetching: isTrainingProfilesLoading } = useTrainingProfiles();
const trainingProfileOptions = computed<SelectOption[]>(() => {
  return (trainingProfiles.value ?? []).map((profile) => ({
    code: profile.id,
    description: profile.name,
  }));
});

const populateFromTraining = (training: TrainingLookupResponse): TrainingFormData => ({
  code: training.code ?? '',
  description: training.description ?? '',
  mandatory: training.mandatory ?? false,
  mandatoryTrainingProfileIds:
    training.mandatoryTrainingProfiles
      ?.map((profile) => profile.id)
      .filter((id): id is number => typeof id === 'number') ?? [],
  validityDayCode: getValidityDayCodeFromDays(training.validityDays),
  advanceNoticeDays: training.advanceNoticeDays == null ? '' : String(training.advanceNoticeDays),
  rotating: training.rotating ?? false,
  trainingCategoryId: training.trainingCategoryId,
});

const formData = ref<TrainingFormData>(populateFromTraining(props.training));
const validityDayOptions = ref(getValidityDayOptions(props.training.validityDays));
const isAnnualValiditySelected = computed(() => formData.value.validityDayCode === annualValidityDayCode);

watch(
  () => props.training,
  (training) => {
    formData.value = populateFromTraining(training);
    validityDayOptions.value = getValidityDayOptions(training.validityDays);
    formErrors.value = {};
    apiErrorMessage.value = '';
  },
  { immediate: true },
);

const categoryDisplay = computed(() => props.training.trainingCategoryName?.trim() || 'Uncategorized');

const parseOptionalNonNegativeNumber = (value: string, fieldName: keyof TrainingFormData): number | null | symbol => {
  const trimmedValue = value.trim();
  if (!trimmedValue) {
    return null;
  }

  const parsedValue = Number(trimmedValue);
  if (!Number.isInteger(parsedValue) || parsedValue < 0) {
    formErrors.value[fieldName] = validationMessages.invalid;
    return Symbol(fieldName);
  }

  return parsedValue;
};

const validateForm = (): TrainingLookupRequest | null => {
  formErrors.value = {};

  const code = formData.value.code.trim();
  const description = (formData.value.description ?? '').trim();

  if (!code) {
    formErrors.value.code = validationMessages.required;
  } else if (code.length > 50) {
    formErrors.value.code = validationMessages.tooLong;
  }

  if (description && description.length > 200) {
    formErrors.value.description = validationMessages.tooLong;
  }

  const validityDays = getValidityDaysFromCode(formData.value.validityDayCode);
  if (validityDays === undefined) {
    formErrors.value.validityDays = validationMessages.invalid;
  }

  const advanceNoticeDays = parseOptionalNonNegativeNumber(formData.value.advanceNoticeDays, 'advanceNoticeDays');

  if (Object.keys(formErrors.value).length > 0) {
    return null;
  }

  return {
    code,
    description,
    mandatory: formData.value.mandatory,
    mandatoryTrainingProfileIds: formData.value.mandatory ? formData.value.mandatoryTrainingProfileIds : [],
    validityDays: validityDays as number | null,
    advanceNoticeDays: advanceNoticeDays as number | null,
    rotating: formData.value.rotating,
    trainingCategoryId: formData.value.trainingCategoryId,
  };
};

const applyServerValidationErrors = (rawError: unknown): boolean => {
  const mappedErrors = mapToValidationErrors(rawError);
  if (!mappedErrors) {
    return false;
  }

  formErrors.value = mappedErrors;
  return true;
};

const handleClose = () => {
  if (!isLoading.value) {
    emit('close');
  }
};

const handleSave = async () => {
  const payload = validateForm();
  if (!payload) {
    return;
  }

  isLoading.value = true;
  apiErrorMessage.value = '';

  try {
    const { data, error } = await putApiLookupTrainingsId(props.training.id, payload);

    if (error.value) {
      if (applyServerValidationErrors(data.value)) {
        return;
      }

      apiErrorMessage.value = error.value.message || 'Failed to update training';
      return;
    }

    emit('updated', data.value ?? null);
    emit('close');
  } catch (err: unknown) {
    apiErrorMessage.value = err instanceof Error ? err.message : 'An unexpected error occurred';
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <UaModal title="Edit Training" :loading="isLoading" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiErrorMessage" type="error" @close="apiErrorMessage = ''">
        Request failed: {{ apiErrorMessage }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <UaTextField
        id="training-code"
        label="Training"
        :model-value="formData.code"
        :error-messages="formErrors.code"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.code = value)"
      />

      <UaTextarea
        id="training-description"
        label="Description"
        :model-value="formData.description ?? ''"
        :error-messages="formErrors.description"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.description = value)"
      />

      <span class="ua-form-label">Validity</span>
      <div class="validity-field">
        <UaSelect
          id="training-validity"
          :items="validityDayOptions"
          v-model="formData.validityDayCode"
          :error-messages="formErrors.validityDays"
          :disabled="isLoading"
        />
        <span v-if="isAnnualValiditySelected" class="validity-field__hint">
          Annual validity expires on Dec 31 of the same calendar year as the awarded date.
        </span>
      </div>

      <UaTextField
        id="training-advance-notice-days"
        label="Advance Notice (Days)"
        type="number"
        min="0"
        step="1"
        :model-value="formData.advanceNoticeDays"
        :error-messages="formErrors.advanceNoticeDays"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.advanceNoticeDays = value)"
      />

      <span class="ua-form-label">Category</span>
      <div class="read-only-field">
        <span>{{ categoryDisplay }}</span>
        <span class="read-only-field__hint">Category editing is not available yet.</span>
      </div>

      <span class="ua-form-label">Mandatory</span>
      <div class="toggle-row">
        <v-switch
          v-model="formData.mandatory"
          color="success"
          hide-details
          material
          base-color="white"
          :disabled="isLoading"
        />
      </div>

      <label class="ua-form-label" for="edit-training-mandatory-profiles">Mandatory Profiles</label>
      <div class="validity-field">
        <div
          v-if="
            isTrainingProfilesLoading &&
            formData.mandatoryTrainingProfileIds.length > 0 &&
            trainingProfileOptions.length === 0
          "
          class="loading-inline"
        >
          <v-progress-circular color="primary" indeterminate size="18" width="2" />
          <span>Loading selected training profiles…</span>
        </div>

        <UaSelect
          v-else
          id="edit-training-mandatory-profiles"
          v-model="formData.mandatoryTrainingProfileIds"
          :items="trainingProfileOptions"
          :loading="isTrainingProfilesLoading"
          :disabled="isLoading || !formData.mandatory"
          :hint="
            formData.mandatory
              ? 'Leave blank to make this mandatory for all users.'
              : 'Enable Mandatory first to restrict by training profile.'
          "
          persistent-hint
          multiple
          chips
          closable-chips
          clearable
        />
      </div>

      <span class="ua-form-label">Rotating</span>
      <div class="toggle-row">
        <v-switch
          v-model="formData.rotating"
          color="success"
          hide-details
          material
          base-color="white"
          :disabled="isLoading"
        />
      </div>
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :prepend-icon="mdiClose" :disabled="isLoading" @click="handleClose">Cancel</UaBtn>
      <UaBtn color="primary" :prepend-icon="mdiContentSave" :loading="isLoading" @click="handleSave">
        Save Changes
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.read-only-field {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
  color: var(--ua-text-primary);
}

.read-only-field__hint {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}

.toggle-row {
  display: flex;
  align-items: center;
  min-height: 40px;
}

.validity-field {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xs);
}

.validity-field__hint {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}

.loading-inline {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-sm);
  min-height: 40px;
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}
</style>
