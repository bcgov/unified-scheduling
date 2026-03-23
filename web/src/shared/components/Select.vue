<script setup lang="ts">
import type { SelectOption, SelectValue } from '@/types/select';

const {
  label = 'Select',
  items,
  multiple = false,
  errorMessages = '',
} = defineProps<{
  label?: string;
  items: SelectOption[];
  multiple?: boolean;
  errorMessages?: string;
}>();

const model = defineModel<SelectValue>();
</script>

<template>
  <v-select
    :label="label"
    :items="items"
    :multiple="multiple"
    v-model="model"
    item-title="description"
    item-value="code"
    hide-details="auto"
    :error-messages="errorMessages"
  />
</template>
