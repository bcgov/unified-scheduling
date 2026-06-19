<script setup lang="ts">
import type { ActingPositionResponseDto } from '@/api-access/acting-positions';
import { postApiUsersIdActingPositionsExpire } from '@/api-access/acting-positions';
import { PostApiUsersUserIdActingPositionsExpireBody } from '@/api-access/generated/acting-positions/acting-positions.zod';
import { USER_ROLE_EXPIRY_REASON_OPTIONS } from '@/constants/ExpiryReasons';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { validationMessages } from '@/shared/validation/validationErrors';
import { ref } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  userId: string;
  position: ActingPositionResponseDto;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'expired'): void;
}>();

const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

type ExpireFormData = Partial<zod.infer<typeof PostApiUsersUserIdActingPositionsExpireBody>>;

const formData = ref<ExpireFormData>({ expiryReason: '' });

const expireSchema = PostApiUsersUserIdActingPositionsExpireBody.extend({
  expiryReason: PostApiUsersUserIdActingPositionsExpireBody.shape.expiryReason.min(
    1,
    validationMessages.required,
  ),
});

const validateForm = (): Pick<zod.infer<typeof expireSchema>, 'expiryReason'> | null => {
  formErrors.value = {};
  const result = expireSchema.safeParse({
    actingPositionId: props.position.id,
    expiryReason: formData.value.expiryReason ?? '',
  });
  if (!result.success) {
    for (const issue of result.error.issues) {
      const field = issue.path[0];
      if (typeof field === 'string' && !formErrors.value[field]) {
        formErrors.value[field] = issue.message;
      }
    }
    return null;
  }
  return result.data;
};

const handleClose = () => {
  if (!isSaving.value) emit('close');
};

const handleExpire = async () => {
  const payload = validateForm();
  if (!payload) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    const { error } = await postApiUsersIdActingPositionsExpire(props.userId, {
      actingPositionId: props.position.id,
      expiryReason: payload.expiryReason,
    });

    if (error.value) {
      apiError.value = error.value.message || 'Failed to expire acting position.';
      return;
    }

    emit('expired');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to expire acting position.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal title="Expire Acting Position" tone="error" :loading="isSaving" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <p>
      Are you sure you want to expire the acting position
      <strong>{{ position.positionTypeDescription }}</strong
      >?
    </p>

    <UaSelect
      v-model="formData.expiryReason"
      label="Reason for Expiry"
      :items="USER_ROLE_EXPIRY_REASON_OPTIONS"
      :error-messages="formErrors.expiryReason"
    />

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="handleClose">Cancel</UaBtn>
      <UaBtn color="error" :loading="isSaving" @click="handleExpire">Expire</UaBtn>
    </template>
  </UaModal>
</template>
