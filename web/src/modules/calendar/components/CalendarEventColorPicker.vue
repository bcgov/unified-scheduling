<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{
  id?: string;
  label?: string;
  modelValue?: string | null;
  colors: Record<string, string>;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: string | null];
}>();

const colorOptions = computed(() =>
  Object.entries(props.colors).map(([code, value]) => ({
    code,
    value,
    label: toColorLabel(code),
  })),
);

function toColorLabel(value: string) {
  return value
    .split(/[-_\s]+/)
    .filter(Boolean)
    .map((part) => `${part.charAt(0).toUpperCase()}${part.slice(1)}`)
    .join(' ');
}

function selectColor(value: string) {
  if (!props.disabled) {
    emit('update:modelValue', value);
  }
}
</script>

<template>
  <fieldset class="calendar-event-color-picker" :aria-labelledby="id ? `${id}-label` : undefined">
    <legend v-if="label" :id="id ? `${id}-label` : undefined" class="calendar-event-color-picker__label">
      {{ label }}
    </legend>
    <div class="calendar-event-color-picker__options">
      <button
        v-for="option in colorOptions"
        :key="option.code"
        class="calendar-event-color-picker__option"
        :class="{ 'is-selected': modelValue === option.code }"
        type="button"
        :aria-label="option.label"
        :aria-pressed="modelValue === option.code"
        :disabled="disabled"
        :title="option.label"
        @click="selectColor(option.code)"
      >
        <span class="calendar-event-color-picker__sphere" :style="{ backgroundColor: option.value }"></span>
      </button>
    </div>
  </fieldset>
</template>

<style scoped>
.calendar-event-color-picker {
  border: 0;
  display: grid;
  gap: var(--ua-spacing-sm);
  margin: 0;
  padding: 0;
}

.calendar-event-color-picker__label {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  padding: 0;
}

.calendar-event-color-picker__options {
  justify-self: center;
  display: grid;
  gap: var(--ua-spacing-sm);
  grid-template-columns: repeat(8, 2rem);
  width: max-content;
}

.calendar-event-color-picker__option {
  align-items: center;
  background: transparent;
  border: 2px solid transparent;
  border-radius: 999px;
  cursor: pointer;
  display: inline-flex;
  height: 2rem;
  justify-content: center;
  padding: 0.125rem;
  width: 2rem;
}

.calendar-event-color-picker__option.is-selected {
  border-color: rgb(var(--v-theme-primary));
}

.calendar-event-color-picker__option:focus-visible {
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: 2px;
}

.calendar-event-color-picker__option:disabled {
  cursor: default;
  opacity: 0.6;
}

.calendar-event-color-picker__sphere {
  border: 1px solid rgb(var(--v-theme-outline));
  border-radius: 999px;
  display: block;
  height: 1.25rem;
  width: 1.25rem;
}
</style>
