<script setup lang="ts">
import type { UserRoleResponseDto } from '@/api-access/generated/models';
import { postApiUsersIdRolesExpire } from '@/api-access/generated/users/users';
import { PostApiUsersIdRolesExpireBody } from '@/api-access/generated/users/users.zod';
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
  assignment: UserRoleResponseDto;
  roleName: string;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

type ExpireRoleFormData = Partial<zod.infer<typeof PostApiUsersIdRolesExpireBody>>;

const formData = ref<ExpireRoleFormData>({ expiryReason: '' });

const expireRoleSchema = PostApiUsersIdRolesExpireBody.extend({
  expiryReason: PostApiUsersIdRolesExpireBody.shape.expiryReason.min(1, validationMessages.required),
});

const validateForm = (): zod.infer<typeof expireRoleSchema> | null => {
  formErrors.value = {};
  const result = expireRoleSchema.safeParse({
    roleId: props.assignment.roleId as number,
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
    const { error } = await postApiUsersIdRolesExpire(props.userId, {
      roleId: payload.roleId,
      expiryReason: payload.expiryReason,
    });

    if (error.value) {
      apiError.value = error.value.message || 'Failed to expire role assignment.';
      return;
    }

    emit('saved');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to expire role assignment.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal title="Expire Role Assignment" tone="error" :loading="isSaving" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <p class="expire-message">
      Are you sure you want to expire the role assignment <strong>{{ roleName }}</strong
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

<style scoped>
.expire-message {
  margin: 0 0 var(--ua-spacing-md) 0;
}
</style>
