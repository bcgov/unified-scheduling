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

type UserTrainingModalMode = 'create' | 'edit' | 'renew';

const props = withDefaults(
  defineProps<{
    userId: string;
    trainingOptions: SelectOption[];
    mode?: UserTrainingModalMode;
    training?: UserTrainingResponse | null;
  }>(),
  {
    mode: 'create',
  },
);

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const isEditMode = computed(() => props.mode === 'edit' && !!props.training);
const isRenewMode = computed(() => props.mode === 'renew' && !!props.training);
const modalTitle = computed(() => {
  if (isEditMode.value) {
    return 'Edit User Training';
  }

  if (isRenewMode.value) {
    return 'Renew User Training';
  }

  return 'Add User Training';
});
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

const createFormData = () => ({
  trainingId: undefined as number | undefined,
  trainingCode: '',
  awardedOn: '',
  endingOn: '',
  expiryDate: '',
  notes: '',
});

const resolveTrainingDisplay = (training: UserTrainingResponse) => {
  if (training.trainingCode?.trim()) {
    return training.trainingCode;
  }

  const option = props.trainingOptions.find((item) => Number(item.code) === training.trainingId);
  return option?.description ?? '';
};

const isSentinelDateInputValue = (value: string | null | undefined) => value === '0001-01-01';

const resolveEndingOnInputValue = (training: UserTrainingResponse) => {
  const endingOn = toDateInputValue(training.endingOn);
  if (endingOn && !isSentinelDateInputValue(endingOn)) {
    return endingOn;
  }

  return toDateInputValue(training.awardedOn) ?? getTodayDateInputValue();
};

const populateFormData = (training: UserTrainingResponse) => ({
  trainingId: training.trainingId,
  trainingCode: resolveTrainingDisplay(training),
  awardedOn: toDateInputValue(training.awardedOn) ?? getTodayDateInputValue(),
  endingOn: resolveEndingOnInputValue(training),
  expiryDate: toDateInputValue(training.expiryDate) ?? '',
  notes: training.notes ?? '',
});

const populateRenewFormData = (training: UserTrainingResponse) => ({
  trainingId: training.trainingId,
  trainingCode: resolveTrainingDisplay(training),
  awardedOn: toDateInputValue(training.awardedOn) ?? getTodayDateInputValue(),
  endingOn: resolveEndingOnInputValue(training),
  expiryDate: toDateInputValue(training.expiryDate) ?? '',
  notes: training.notes ?? '',
});

const getInitialFormData = () => {
  if (!props.training) {
    return createFormData();
  }

  switch (props.mode) {
    case 'edit':
      return populateFormData(props.training);
    case 'renew':
      return populateRenewFormData(props.training);
    default:
      return createFormData();
  }
};

const formData = ref(getInitialFormData());

const schema = zod.object({
  trainingId: zod.number({ error: 'Training is required.' }),
  awardedOn: zod.string().min(1, 'From date is required.'),
  endingOn: zod.string().min(1, 'To date is required.'),
  expiryDate: zod.string(),
  notes: zod.string(),
});

watch(
  () => [props.training, props.mode] as const,
  ([training, mode]) => {
    if (!training) {
      formData.value = createFormData();
    } else if (mode === 'edit') {
      formData.value = populateFormData(training);
    } else if (mode === 'renew') {
      formData.value = populateRenewFormData(training);
    } else {
      formData.value = createFormData();
    }

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
    endingOn: toOffsetDateTimeString(parsed.data.endingOn, '', 'America/Vancouver'),
    expiryDate: parsed.data.expiryDate
      ? toOffsetDateTimeString(parsed.data.expiryDate, '23:59', 'America/Vancouver')
      : null,
    notes: parsed.data.notes.trim() || null,
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
      <label v-if="!isEditMode && !isRenewMode" class="ua-form-label" for="user-training-training">Training</label>
      <UaSelect
        v-if="!isEditMode && !isRenewMode"
        id="user-training-training"
        label="Training"
        :items="props.trainingOptions"
        v-model="formData.trainingId"
        :error-messages="formErrors.trainingId"
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
        label="From"
        :error-messages="formErrors.awardedOn"
      />

      <UaTextField
        id="user-training-ending-on"
        v-model="formData.endingOn"
        type="date"
        label="To"
        :error-messages="formErrors.endingOn"
      />

      <UaTextField
        id="user-training-expiry-date"
        v-model="formData.expiryDate"
        type="date"
        label="Expiry Date"
        :error-messages="formErrors.expiryDate"
      />

      <UaTextarea id="user-training-notes" v-model="formData.notes" label="Notes" />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>
