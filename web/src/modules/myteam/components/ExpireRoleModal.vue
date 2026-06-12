<script setup lang="ts">
import type { UserRoleResponseDto } from '@/api-access/generated/models';
import { postApiUsersIdRolesExpire } from '@/api-access/generated/users/users';
import { USER_ROLE_EXPIRY_REASON_OPTIONS } from '@/constants/ExpiryReasons';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';

import { ref } from 'vue';

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
const selectedReason = ref('');
const formError = ref('');

const reasonOptions = USER_ROLE_EXPIRY_REASON_OPTIONS;

const clearErrors = (): void => {
  apiError.value = '';
  formError.value = '';
};

const handleClose = () => {
  if (isSaving.value) {
    return;
  }

  clearErrors();
  emit('close');
};

const handleExpire = async () => {
  clearErrors();

  if (!selectedReason.value) {
    formError.value = 'Reason for expiry is required.';
    return;
  }

  isSaving.value = true;

  try {
    const { error } = await postApiUsersIdRolesExpire(props.userId, {
      roleId: props.assignment.roleId as number,
      expiryReason: selectedReason.value,
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
      <UaAlert v-if="formError" type="error" @close="formError = ''">
        {{ formError }}
      </UaAlert>
    </template>

    <p class="expire-message">
      Are you sure you want to expire the role assignment <strong>{{ roleName }}</strong
      >?
    </p>

    <UaSelect v-model="selectedReason" label="Reason for Expiry" :items="reasonOptions" />

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
