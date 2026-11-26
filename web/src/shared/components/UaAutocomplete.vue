<script setup lang="ts">
import type { SelectOption, SelectValue } from '@/types/select';

defineOptions({ inheritAttrs: false });

const props = withDefaults(
  defineProps<{
    items: SelectOption[];
    loading?: boolean;
    clientFilter?: boolean;
    browserAutocomplete?: 'on' | 'off';
  }>(),
  {
    browserAutocomplete: 'on',
  },
);
const props = withDefaults(
  defineProps<{
    items: SelectOption[];
    loading?: boolean;
    clientFilter?: boolean;
    browserAutocomplete?: 'on' | 'off';
  }>(),
  {
    browserAutocomplete: 'on',
  },
);

const emit = defineEmits<{
  'update:search': [value: string];
}>();

const model = defineModel<SelectValue>();
</script>

<template>
  <v-autocomplete
    v-bind="$attrs"
    :items="props.items"
    :loading="props.loading"
    :no-filter="!props.clientFilter"
    :autocomplete="props.browserAutocomplete"
    clear-on-select
    v-model="model"
    item-title="description"
    item-value="code"
    hide-details="auto"
    single-line
    @update:search="emit('update:search', $event)"
  >
    <template v-if="$slots['prepend-item']" #prepend-item>
      <slot name="prepend-item" />
    </template>
    <template v-if="$slots['prepend-inner']" #prepend-inner>
      <slot name="prepend-inner" />
    </template>
  </v-autocomplete>
</template>
