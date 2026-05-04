<script setup lang="ts">
defineOptions({ inheritAttrs: false });

defineProps<{
  /** Visible label text for the field. */
  label: string;
  /** HTML id for the input element, used for label association. */
  id: string;
  errorMessages?: string;
  disabled?: boolean;
  placeholder?: string;
  modelValue?: string;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();
</script>

<template>
  <label class="ua-form-label" :for="id">{{ label }}</label>
  <v-textarea
    :id="id"
    :model-value="modelValue"
    :placeholder="placeholder || label"
    :error-messages="errorMessages"
    :disabled="disabled"
    hide-details="auto"
    v-bind="$attrs"
    @update:model-value="(v: string) => emit('update:modelValue', String(v))"
  />
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}
</style>
