<script setup lang="ts">
withDefaults(
  defineProps<{
    type?: 'success' | 'error' | 'warning' | 'info';
    closable?: boolean;
  }>(),
  {
    type: 'info',
    closable: true,
  },
);

const emit = defineEmits<{
  (e: 'close'): void;
}>();
</script>

<template>
  <v-alert :type="type" density="compact" class="ua-alert" :closable="closable" @click:close="emit('close')">
    <slot />
  </v-alert>
</template>

<style scoped>
.ua-alert {
  margin-bottom: var(--ua-spacing-md);
}
</style>
