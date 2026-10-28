<script setup lang="ts">
import type { SelectOption, SelectValue } from '@/types/select';
import UaRequiredIndicator from './UaRequiredIndicator.vue';

defineOptions({ inheritAttrs: false });

const { items } = defineProps<{
  items: SelectOption[];
  clearable?: boolean;
  persistentClear?: boolean;
  /** Display and announce required status without changing form validation. */
  required?: boolean;
}>();

const model = defineModel<SelectValue>();
</script>

<template>
  <v-select
    :aria-required="required || undefined"
    v-bind="$attrs"
    :clearable="clearable"
    :persistent-clear="persistentClear"
    :items="items"
    v-model="model"
    item-title="description"
    item-value="code"
    hide-details="auto"
    single-line
  >
    <template v-if="required && $attrs.label" #label> {{ $attrs.label }}<UaRequiredIndicator /> </template>
  </v-select>
</template>
