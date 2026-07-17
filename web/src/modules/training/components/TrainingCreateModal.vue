<script setup lang="ts">
import { postApiLookupTrainings } from '@/api-access/generated/training/training';
import type { TrainingLookupRequest, TrainingLookupResponse } from '@/api-access/generated/models';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { mdiClose, mdiContentSave } from '@mdi/js';
import { ref } from 'vue';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'created', training: TrainingLookupResponse | null): void;
}>();

type TrainingCreateFormData = {
  code: string;
  description: string;
  mandatory: boolean;
  validityDays: string;
  advanceNoticeDays: string;
  rotating: boolean;
};

const formData = ref<TrainingCreateFormData>({
  code: '',
  description: '',
  mandatory: false,
  validityDays: '',
  advanceNoticeDays: '',
  rotating: false,
});

const isLoading = ref(false);
const apiErrorMessage = ref('');
const formErrors = ref<Record<string, string>>({});

const parseOptionalNonNegativeNumber = (
  value: string,
  fieldName: keyof TrainingCreateFormData,
): number | null | symbol => {
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
  const description = formData.value.description.trim();

  if (!code) {
    formErrors.value.code = validationMessages.required;
  } else if (code.length > 50) {
    formErrors.value.code = validationMessages.tooLong;
  }

  if (description && description.length > 200) {
    formErrors.value.description = validationMessages.tooLong;
  }

  const validityDays = parseOptionalNonNegativeNumber(formData.value.validityDays, 'validityDays');
  const advanceNoticeDays = parseOptionalNonNegativeNumber(formData.value.advanceNoticeDays, 'advanceNoticeDays');

  if (Object.keys(formErrors.value).length > 0) {
    return null;
  }

  return {
    code,
    description,
    mandatory: formData.value.mandatory,
    validityDays: validityDays as number | null,
    advanceNoticeDays: advanceNoticeDays as number | null,
    rotating: formData.value.rotating,
    trainingCategoryId: null,
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
    const { data, error } = await postApiLookupTrainings(payload);

    if (error.value) {
      if (applyServerValidationErrors(data.value)) {
        return;
      }

      apiErrorMessage.value = error.value.message || 'Failed to create training';
      return;
    }

    emit('created', data.value ?? null);
    emit('close');
  } catch (err: unknown) {
    apiErrorMessage.value = err instanceof Error ? err.message : 'An unexpected error occurred';
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <UaModal title="Create Training" :loading="isLoading" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiErrorMessage" type="error" @close="apiErrorMessage = ''">
        Request failed: {{ apiErrorMessage }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <UaTextField
        id="create-training-code"
        label="Training"
        :model-value="formData.code"
        :error-messages="formErrors.code"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.code = value)"
      />

      <UaTextarea
        id="create-training-description"
        label="Description"
        :model-value="formData.description"
        :error-messages="formErrors.description"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.description = value)"
      />

      <UaTextField
        id="create-training-validity-days"
        label="Validity (Days)"
        type="number"
        min="0"
        step="1"
        :model-value="formData.validityDays"
        :error-messages="formErrors.validityDays"
        :disabled="isLoading"
        @update:model-value="(value: string) => (formData.validityDays = value)"
      />

      <UaTextField
        id="create-training-advance-notice-days"
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
        <span>Uncategorized</span>
        <span class="read-only-field__hint">Category selection is not available yet.</span>
      </div>

      <span class="ua-form-label">Mandatory</span>
      <div class="toggle-row">
        <v-switch
          v-model="formData.mandatory"
          color="success"
          hide-details
          material
          :disabled="isLoading"
          base-color="white"
        />
      </div>

      <span class="ua-form-label">Rotating</span>
      <div class="toggle-row">
        <v-switch
          v-model="formData.rotating"
          color="success"
          hide-details
          material
          :disabled="isLoading"
          base-color="white"
        />
      </div>
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :prepend-icon="mdiClose" :disabled="isLoading" @click="handleClose">Cancel</UaBtn>
      <UaBtn color="primary" :prepend-icon="mdiContentSave" :loading="isLoading" @click="handleSave">
        Create Training
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
</style>
