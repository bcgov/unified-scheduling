<script setup lang="ts">
import { type UserTrainingRequest, type UserTrainingResponse } from '@/api-access/generated/models';
import {
  getApiTrainingUserTrainingsExpiryDate,
  postApiTrainingUserTrainings,
  putApiTrainingUserTrainingsId,
} from '@/api-access/generated/user-training/user-training';
import { PostApiTrainingUserTrainingsBody } from '@/api-access/generated/user-training/user-training.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { getTodayDateInputValue, toDateInputValue, toDateTime, toOffsetDateTimeString } from '@/utils/date';
import type { SelectOption } from '@/types/select';
import { mdiRefresh } from '@mdi/js';
import { DateTime } from 'luxon';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';

type UserTrainingModalMode = 'create' | 'edit' | 'renew';
type UserTrainingFormData = Partial<zod.infer<typeof PostApiTrainingUserTrainingsBody>> & {
  trainingCode: string;
  awardedTime: string;
  endingTime: string;
  expiryDate: string;
  notes: string;
};

const trainingTimeZone = 'America/Vancouver';

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
const isGeneratingExpiryDate = ref(false);
const apiError = ref('');
const expiryDateAutoGenerateError = ref('');
const formErrors = ref<Record<string, string>>({});
const hasManualExpiryDateOverride = ref(false);

const createFormData = (): UserTrainingFormData => ({
  trainingId: undefined as number | undefined,
  trainingCode: '',
  awardedOn: '',
  awardedTime: '',
  endingOn: '',
  endingTime: '',
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

const resolveEndingOn = (training: UserTrainingResponse) => {
  const endingOn = toDateInputValue(training.endingOn);
  if (endingOn && !isSentinelDateInputValue(endingOn)) {
    return training.endingOn!;
  }

  return training.awardedOn;
};

const getTrainingDate = (value: string) => toDateTime(value, trainingTimeZone).toISODate() ?? getTodayDateInputValue();
const getTrainingTime = (value: string) => {
  const dateTime = toDateTime(value, trainingTimeZone);
  return dateTime.isValid ? dateTime.toFormat('HH:mm') : '';
};

const populateFormData = (training: UserTrainingResponse): UserTrainingFormData => ({
  trainingId: training.trainingId,
  trainingCode: resolveTrainingDisplay(training),
  awardedOn: getTrainingDate(training.awardedOn),
  awardedTime: getTrainingTime(training.awardedOn),
  endingOn: getTrainingDate(resolveEndingOn(training)),
  endingTime: getTrainingTime(resolveEndingOn(training)),
  expiryDate: toDateInputValue(training.expiryDate) ?? '',
  notes: training.notes ?? '',
});

const populateRenewFormData = populateFormData;

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

const canGenerateExpiryDate = computed(() => Boolean(formData.value.trainingId && formData.value.endingOn));

const expiryDateErrorMessages = computed(() => formErrors.value.expiryDate || expiryDateAutoGenerateError.value || '');

const schema = PostApiTrainingUserTrainingsBody.omit({ userId: true })
  .extend({
    trainingId: zod.number({ error: 'Training is required.' }),
    trainingCode: zod.string(),
    awardedOn: zod.iso.date({ error: 'From date is required.' }),
    endingOn: zod.iso.date({ error: 'To date is required.' }),
    awardedTime: zod.union([zod.literal(''), zod.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'Invalid from time.')]),
    endingTime: zod.union([zod.literal(''), zod.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'Invalid to time.')]),
    expiryDate: zod.string(),
    notes: zod.string(),
  })
  .superRefine((data, context) => {
    const start = DateTime.fromISO(`${data.awardedOn}T${data.awardedTime || '00:00'}`, { zone: trainingTimeZone });
    const end = DateTime.fromISO(`${data.endingOn}T${data.endingTime || '00:00'}`, { zone: trainingTimeZone });
    if (start.isValid && end.isValid && end < start) {
      context.addIssue({ code: 'custom', path: ['endingOn'], message: 'To date and time must be on or after From.' });
    }
  });

watch(
  () => [props.training, props.mode] as const,
  ([training, mode]) => {
    if (!training) {
      formData.value = createFormData();
      hasManualExpiryDateOverride.value = false;
    } else if (mode === 'edit') {
      formData.value = populateFormData(training);
      hasManualExpiryDateOverride.value = true;
    } else if (mode === 'renew') {
      formData.value = populateRenewFormData(training);
      hasManualExpiryDateOverride.value = true;
    } else {
      formData.value = createFormData();
      hasManualExpiryDateOverride.value = false;
    }

    apiError.value = '';
    expiryDateAutoGenerateError.value = '';
    formErrors.value = {};
  },
);

const handleExpiryDateInput = (value: string) => {
  formData.value.expiryDate = value;
  hasManualExpiryDateOverride.value = true;
  expiryDateAutoGenerateError.value = '';
  delete formErrors.value.expiryDate;
};

const generateExpiryDate = async (force = false) => {
  if (!canGenerateExpiryDate.value) {
    if (!hasManualExpiryDateOverride.value || force) {
      formData.value.expiryDate = '';
    }
    return;
  }

  if (!force && hasManualExpiryDateOverride.value) {
    return;
  }

  isGeneratingExpiryDate.value = true;
  expiryDateAutoGenerateError.value = '';

  try {
    const requestedTrainingId = formData.value.trainingId!;
    const requestedEndingOn = formData.value.endingOn!;
    const requestedEndingTime = formData.value.endingTime;

    const { data, error, execute } = getApiTrainingUserTrainingsExpiryDate(
      {
        TrainingId: requestedTrainingId,
        AwardedOn: toOffsetDateTimeString(requestedEndingOn, requestedEndingTime, trainingTimeZone),
      },
      {
        options: {
          immediate: false,
        },
      },
    );

    await execute();

    if (error.value) {
      throw error.value;
    }

    const expiryDate = data.value?.expiryDate ?? null;

    if (
      requestedTrainingId !== formData.value.trainingId ||
      requestedEndingOn !== formData.value.endingOn ||
      requestedEndingTime !== formData.value.endingTime
    ) {
      return;
    }

    formData.value.expiryDate = toDateInputValue(expiryDate) ?? '';
    hasManualExpiryDateOverride.value = false;
  } catch (error: unknown) {
    expiryDateAutoGenerateError.value = error instanceof Error ? error.message : 'Failed to auto-generate expiry date.';
  } finally {
    isGeneratingExpiryDate.value = false;
  }
};

const handleGenerateExpiryDate = async () => {
  hasManualExpiryDateOverride.value = false;
  await generateExpiryDate(true);
};

watch(
  () => [formData.value.trainingId, formData.value.endingOn, formData.value.endingTime] as const,
  () => {
    void generateExpiryDate();
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
    awardedOn: toOffsetDateTimeString(parsed.data.awardedOn, parsed.data.awardedTime, trainingTimeZone),
    endingOn: toOffsetDateTimeString(parsed.data.endingOn, parsed.data.endingTime, trainingTimeZone),
    expiryDate: parsed.data.expiryDate ? toOffsetDateTimeString(parsed.data.expiryDate, '', trainingTimeZone) : null,
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
        id="user-training-awarded-time"
        v-model="formData.awardedTime"
        type="time"
        label="From Time (optional)"
        :error-messages="formErrors.awardedTime"
      />

      <UaTextField
        id="user-training-ending-on"
        v-model="formData.endingOn"
        type="date"
        label="To"
        :error-messages="formErrors.endingOn"
      />

      <UaTextField
        id="user-training-ending-time"
        v-model="formData.endingTime"
        type="time"
        label="To Time (optional)"
        :error-messages="formErrors.endingTime"
      />

      <label class="ua-form-label" for="user-training-expiry-date">Expiry Date</label>
      <div class="user-training-modal__expiry-row">
        <UaTextField
          id="user-training-expiry-date"
          class="user-training-modal__expiry-input"
          :model-value="formData.expiryDate"
          type="date"
          label=""
          :error-messages="expiryDateErrorMessages"
          @update:model-value="handleExpiryDateInput"
        />
        <UaBtn
          class="user-training-modal__generate-expiry-btn"
          variant="tonal"
          color="primary"
          :prepend-icon="mdiRefresh"
          :disabled="!canGenerateExpiryDate || isGeneratingExpiryDate"
          :loading="isGeneratingExpiryDate"
          @click="handleGenerateExpiryDate"
        >
          Generate expiry
        </UaBtn>
      </div>

      <UaTextarea id="user-training-notes" v-model="formData.notes" label="Notes" />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.user-training-modal__expiry-row {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-sm);
}

.user-training-modal__expiry-input {
  flex: 1;
}

.user-training-modal__generate-expiry-btn {
  margin-top: -2px;
  white-space: nowrap;
  font-weight: var(--ua-font-weight-medium);
}

@media (max-width: 640px) {
  .user-training-modal__expiry-row {
    flex-direction: column;
    align-items: stretch;
  }

  .user-training-modal__generate-expiry-btn {
    align-self: flex-start;
    margin-top: 0;
  }
}
</style>
