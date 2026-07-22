<script setup lang="ts">
import { type UserTrainingRequest, type UserTrainingResponse } from '@/api-access/generated/models';
import {
  postApiTrainingUserTrainings,
  putApiTrainingUserTrainingsId,
} from '@/api-access/generated/user-training/user-training';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { getTodayDateInputValue, toDateInputValue, toOffsetDateTimeString } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  userId: string;
  trainingOptions: SelectOption[];
  training?: UserTrainingResponse | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const isEditMode = computed(() => !!props.training);
const modalTitle = computed(() => (isEditMode.value ? 'Edit User Training' : 'Add User Training'));
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

const createFormData = () => ({
  trainingId: undefined as number | undefined,
  trainingCode: '',
  awardedOn: getTodayDateInputValue(),
  expiryDate: '',
  notes: '',
  overrideConflicts: false,
  allowConflictingEvents: false,
});

const populateFormData = (training: UserTrainingResponse) => ({
  trainingId: training.trainingId,
  awardedOn: toDateInputValue(training.awardedOn) ?? getTodayDateInputValue(),
  expiryDate: toDateInputValue(training.expiryDate) ?? '',
  notes: training.notes ?? '',
  overrideConflicts: false,
  allowConflictingEvents: false,
  trainingCode: training.trainingCode ?? '',
});

const formData = ref(props.training ? populateFormData(props.training) : createFormData());

const schema = zod.object({
  trainingId: zod.number({ error: 'Training is required.' }),
  awardedOn: zod.string().min(1, 'Awarded on date is required.'),
  expiryDate: zod.string(),
  notes: zod.string(),
  overrideConflicts: zod.boolean(),
  allowConflictingEvents: zod.boolean(),
});

watch(
  () => props.training,
  (training) => {
    formData.value = training ? populateFormData(training) : createFormData();
    apiError.value = '';
    formErrors.value = {};
  },
);

const buildRequest = (): UserTrainingRequest | null => {
  formErrors.value = {};
  const parsed = schema.safeParse(formData.value);

  if (!parsed.success) {
    for (const issue of parsed.error.issues) {
      const field = issue.path[0];
      if (typeof field === 'string' && !formErrors.value[field]) {
        formErrors.value[field] = issue.message;
      }
    }
    return null;
  }

  return {
    userId: props.userId,
    trainingId: parsed.data.trainingId,
    awardedOn: toOffsetDateTimeString(parsed.data.awardedOn, '', 'America/Vancouver'),
    expiryDate: parsed.data.expiryDate
      ? toOffsetDateTimeString(parsed.data.expiryDate, '23:59', 'America/Vancouver')
      : null,
    notes: parsed.data.notes.trim() || null,
    overrideConflicts: parsed.data.overrideConflicts,
    allowConflictingEvents: parsed.data.allowConflictingEvents,
  };
};

const handleSave = async () => {
  const request = buildRequest();
  if (!request) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    const result =
      isEditMode.value && props.training
        ? await putApiTrainingUserTrainingsId(props.training.id, request)
        : await postApiTrainingUserTrainings(request);

    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to save training record.';
      return;
    }

    emit('saved');
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to save training record.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal :title="modalTitle" :loading="isSaving" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <!-- <UaSelect
        v-if="!isEditMode"
        id="user-training-training"
        v-model="formData.trainingCode"
        label="Training"
        type="select"
        :items="props.trainingOptions"
        :options="props.trainingOptions"
        :error-messages="formErrors.trainingCode"
      /> -->
      <label v-if="!isEditMode" class="ua-form-label" for="user-training-training">Training</label>
      <UaSelect
        v-if="!isEditMode"
        id="user-training-training"
        label="Training"
        :items="props.trainingOptions"
        :model-value="formData.trainingCode"
      />
      <UaTextField
        v-else
        id="user-training-type"
        v-model="formData.trainingCode"
        label="Training"
        type="text"
        disabled
      />

      <UaTextField
        id="user-training-awarded-on"
        v-model="formData.awardedOn"
        type="date"
        label="Awarded On"
        :error-messages="formErrors.awardedOn"
      />

      <UaTextField
        id="user-training-expiry-date"
        v-model="formData.expiryDate"
        type="date"
        label="Expiry Date"
        :error-messages="formErrors.expiryDate"
      />

      <UaTextarea id="user-training-notes" v-model="formData.notes" label="Notes" />

      <label class="ua-form-label" for="user-training-override">Versioning</label>
      <div>
        <v-checkbox
          id="user-training-override"
          v-model="formData.overrideConflicts"
          label="Supersede active record and keep history"
          density="compact"
          hide-details
        />
        <v-checkbox
          v-model="formData.allowConflictingEvents"
          label="Allow overlapping training records"
          density="compact"
          hide-details
        />
      </div>
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>
