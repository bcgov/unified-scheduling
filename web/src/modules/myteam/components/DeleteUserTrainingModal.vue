<script setup lang="ts">
import { type UserTrainingResponse } from '@/api-access/generated/models';
import { deleteApiTrainingUserTrainingsId } from '@/api-access/generated/user-training/user-training';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { ref } from 'vue';

const props = defineProps<{
  training: UserTrainingResponse;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

const isDeleting = ref(false);
const apiError = ref('');

const handleDelete = async () => {
  isDeleting.value = true;
  apiError.value = '';

  try {
    const { error } = await deleteApiTrainingUserTrainingsId(props.training.id);
    if (error.value) {
      apiError.value = error.value.message || 'Failed to delete training record.';
      return;
    }

    emit('deleted');
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'Failed to delete training record.';
  } finally {
    isDeleting.value = false;
  }
};
</script>

<template>
  <UaModal title="Delete User Training" tone="error" :loading="isDeleting" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <p>
      Are you sure you want to delete
      <strong>{{ training.trainingCode }}</strong>
      awarded on
      <strong>{{ training.awardedOn.slice(0, 10) }}</strong>
      ?
    </p>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isDeleting" @click="emit('close')">Cancel</UaBtn>
      <UaBtn color="error" :loading="isDeleting" @click="handleDelete">Delete</UaBtn>
    </template>
  </UaModal>
</template>
