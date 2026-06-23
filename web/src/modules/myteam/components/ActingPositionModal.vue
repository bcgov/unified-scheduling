<script setup lang="ts">
import type { ActingPositionRequestDto, ActingPositionResponseDto } from '@/api-access/generated/models';
import {
  postApiUsersUserIdActingPositions,
  putApiUsersUserIdActingPositionsActingPositionId,
} from '@/api-access/generated/acting-positions/acting-positions';
import { PostApiUsersUserIdActingPositionsBody } from '@/api-access/generated/acting-positions/acting-positions.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import type { SelectOption } from '@/types/select';
import { validationMessages } from '@/shared/validation/validationErrors';
import { getTodayDateInputValue, isDateInputBefore, toApiDateString, toDateInputValue } from '@/utils/date';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  userId: string;
  positionTypes: SelectOption[];
  position?: ActingPositionResponseDto | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const isEditMode = computed(() => !!props.position);
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

const modalTitle = computed(() => (isEditMode.value ? 'Edit Acting Position' : 'Add Acting Position'));

type ActingPositionFormData = Partial<zod.infer<typeof PostApiUsersUserIdActingPositionsBody>>;

const createInitialFormData = (): ActingPositionFormData => ({
  positionTypeCode: undefined,
  effectiveDate: getTodayDateInputValue(),
  expiryDate: null,
  comment: null,
});

const populateFromPosition = (pos: ActingPositionResponseDto): ActingPositionFormData => ({
  positionTypeCode: pos.positionTypeCode,
  effectiveDate: toDateInputValue(pos.effectiveDate) ?? getTodayDateInputValue(),
  expiryDate: toDateInputValue(pos.expiryDate) ?? null,
  comment: pos.comment ?? null,
});

const formData = ref<ActingPositionFormData>(
  props.position ? populateFromPosition(props.position) : createInitialFormData(),
);

const actingPositionSchema = PostApiUsersUserIdActingPositionsBody.extend({
  positionTypeCode: zod.string({ error: validationMessages.required }).min(1, validationMessages.required),
  effectiveDate: PostApiUsersUserIdActingPositionsBody.shape.effectiveDate.min(1, validationMessages.required),
}).superRefine((data, ctx) => {
  if (data.expiryDate && data.effectiveDate && isDateInputBefore(data.expiryDate, data.effectiveDate)) {
    ctx.addIssue({
      code: 'custom',
      path: ['expiryDate'],
      message: 'Expiry date cannot be earlier than effective date.',
    });
  }
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      errors[fieldName] = issue.message;
    }
  }
  return errors;
};

watch(
  () => props.position,
  (pos) => {
    formData.value = pos ? populateFromPosition(pos) : createInitialFormData();
    apiError.value = '';
    formErrors.value = {};
  },
);

function validateForm(): ActingPositionRequestDto | null {
  formErrors.value = {};
  const result = actingPositionSchema.safeParse({
    ...formData.value,
    expiryDate: formData.value.expiryDate || null,
    comment: formData.value.comment?.trim() || null,
  });

  if (!result.success) {
    formErrors.value = getFieldErrors(result.error);
    return null;
  }

  return {
    positionTypeCode: result.data.positionTypeCode,
    effectiveDate: toApiDateString(result.data.effectiveDate),
    expiryDate: result.data.expiryDate ? toApiDateString(result.data.expiryDate) : null,
    comment: result.data.comment ?? null,
  };
}

const handleSave = async () => {
  const request = validateForm();
  if (!request) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    if (isEditMode.value && props.position) {
      const { error } = await putApiUsersUserIdActingPositionsActingPositionId(
        props.userId,
        props.position.id!,
        request,
      );

      if (error.value) {
        apiError.value = error.value.message || 'Failed to save acting position.';
        return;
      }
    } else {
      const { error } = await postApiUsersUserIdActingPositions(props.userId, request);
      if (error.value) {
        apiError.value = error.value.message || 'Failed to save acting position.';
        return;
      }
    }

    emit('saved');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to save acting position.';
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
      <label for="acting-position-type">Position Type</label>
      <UaSelect
        id="acting-position-type"
        v-model="formData.positionTypeCode"
        label="Position Type"
        :items="props.positionTypes"
        :disabled="isEditMode"
        :error-messages="formErrors.positionTypeCode"
      />

      <UaTextField
        id="acting-position-effective-date"
        v-model="formData.effectiveDate"
        label="Effective Date"
        type="date"
        :error-messages="formErrors.effectiveDate"
      />

      <UaTextField
        id="acting-position-expiry-date"
        v-model="formData.expiryDate"
        label="Expiry Date (optional)"
        type="date"
        :error-messages="formErrors.expiryDate"
      />

      <UaTextarea
        id="acting-position-comment"
        label="Comment (optional)"
        :model-value="formData.comment ?? ''"
        @update:model-value="(v: string) => (formData.comment = v)"
      />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>
