<script setup lang="ts">
import type { SelectOption, SelectValue } from '@/types/select';

defineOptions({ inheritAttrs: false });

const { items } = defineProps<{
  items: SelectOption[];
}>();

const model = defineModel<SelectValue>();
</script>

<template>
  <v-select
    v-bind="$attrs"
    :items="items"
    v-model="model"
    item-title="description"
    item-value="code"
    hide-details="auto"
    single-line
  />
</template>
