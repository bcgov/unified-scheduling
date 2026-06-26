<script setup lang="ts" generic="T extends string | number | null = string">
defineOptions({ inheritAttrs: false });

defineProps<{
  /** Visible label text for the field. */
  label: string;
  /** HTML id for the input element, used for label association. */
  id: string;
  errorMessages?: string;
  disabled?: boolean;
  placeholder?: string;
  type?: string;
  modelValue?: T;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: T): void;
}>();

const updateModelValue = (value: string) => {
  emit('update:modelValue', value as T);
};
</script>

<template>
  <label v-if="label || $slots.label" class="ua-form-label" :for="id">
    <slot name="label">{{ label }}</slot>
  </label>
  <v-text-field
    :id="id"
    :model-value="modelValue"
    :placeholder="placeholder || label"
    :type="type"
    :error-messages="errorMessages"
    :disabled="disabled"
    hide-details="auto"
    v-bind="$attrs"
    @update:model-value="updateModelValue"
  />
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}
</style>
