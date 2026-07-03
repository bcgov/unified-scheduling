<script setup lang="ts">
import UaModal from '@/shared/components/UaModal.vue';

const emit = defineEmits<{
  close: [];
}>();
</script>

<template>
  <UaModal title="Add Assignment" width="520" @close="emit('close')">
    <p class="calendar-scheduling-assignment-modal__text">enter assignment details</p>
  </UaModal>
</template>

<style scoped>
.calendar-scheduling-assignment-modal__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}
</style>
