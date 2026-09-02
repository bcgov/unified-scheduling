<script setup lang="ts">
import type { SelectOption, SelectValue } from '@/types/select';

defineOptions({ inheritAttrs: false });

const { items } = defineProps<{
  items: SelectOption[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  'update:search': [value: string];
}>();

const model = defineModel<SelectValue>();
</script>

<template>
  <v-autocomplete
    v-bind="$attrs"
    :items="items"
    :loading="loading"
    v-model="model"
    item-title="description"
    item-value="code"
    hide-details="auto"
    no-filter
    single-line
    @update:search="emit('update:search', $event)"
  />
</template>
