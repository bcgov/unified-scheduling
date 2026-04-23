<script setup lang="ts">
defineProps<{
  /** Visible label text for the field. */
  label: string;
  /** HTML id for the input element, used for label association. */
  id: string;
  errorMessages?: string;
  disabled?: boolean;
  placeholder?: string;
  type?: string;
  modelValue?: string | number | null;
}>();

defineEmits<{
  (e: 'update:modelValue', value: string): void;
}>();
</script>

<template>
  <label class="ua-form-label" :for="id">{{ label }}</label>
  <v-text-field
    :id="id"
    :model-value="modelValue"
    :placeholder="placeholder || label"
    :type="type"
    :error-messages="errorMessages"
    :disabled="disabled"
    hide-details="auto"
    @update:model-value="$emit('update:modelValue', $event)"
  />
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}
</style>
