<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(
  defineProps<{
    label: string;
    value?: string | number | null;
    emptyText?: string;
  }>(),
  {
    value: undefined,
    emptyText: '—',
  },
);

const displayValue = computed(() => {
  if (props.value === null || props.value === undefined || props.value === '') {
    return props.emptyText;
  }

  return String(props.value);
});
</script>

<template>
  <span class="ua-form-label">{{ label }}</span>
  <div class="ua-display-field__value">
    <slot>{{ displayValue }}</slot>
  </div>
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.ua-display-field__value {
  color: var(--ua-text-primary);
  white-space: pre-wrap;
  word-break: break-word;
}
</style>
