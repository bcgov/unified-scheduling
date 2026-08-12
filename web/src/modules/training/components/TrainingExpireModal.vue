<script setup lang="ts">
import type { TrainingLookupResponse } from '@/api-access/generated/models';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { computed } from 'vue';

const props = defineProps<{
  training: TrainingLookupResponse;
  mode: 'expire' | 'unexpire';
  loading?: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'confirm'): void;
}>();

const isUnexpireMode = computed(() => props.mode === 'unexpire');
const title = computed(() => (isUnexpireMode.value ? 'Unexpire Training' : 'Expire Training'));
const actionLabel = computed(() => (isUnexpireMode.value ? 'Unexpire' : 'Expire'));
const descriptionText = computed(() =>
  isUnexpireMode.value
    ? 'This will make it selectable again for new user training entries.'
    : 'This will hide it from active training options for new user training entries.',
);
</script>

<template>
  <UaModal :title="title" tone="warning" :loading="loading" @close="emit('close')">
    <p>
      Are you sure you want to {{ actionLabel.toLowerCase() }}
      <strong>{{ training.code }}</strong>
      ?
    </p>
    <p>{{ descriptionText }}</p>

    <template #actions>
      <UaBtn variant="outlined" :disabled="loading" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :color="isUnexpireMode ? 'success' : 'warning'" :loading="loading" @click="emit('confirm')">
        {{ actionLabel }}
      </UaBtn>
    </template>
  </UaModal>
</template>
