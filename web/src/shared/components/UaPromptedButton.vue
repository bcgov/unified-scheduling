<script setup lang="ts">
import { mergeProps } from 'vue';
import UaActionPrompt from './UaActionPrompt.vue';
import UaBtn from './UaBtn.vue';

defineOptions({ inheritAttrs: false });

defineProps<{
  prompt?: string;
  disabled?: boolean;
  variant?: 'text' | 'outlined' | 'flat';
}>();

const emit = defineEmits<{
  (event: 'click'): void;
}>();
</script>

<template>
  <UaActionPrompt :prompt="prompt">
    <template #activator="{ tooltipProps, highlighted }">
      <UaBtn
        v-bind="mergeProps(tooltipProps, $attrs)"
        :class="{ 'ua-action-prompt--highlighted': highlighted }"
        :disabled="disabled"
        :variant="variant ?? 'outlined'"
        @click="emit('click')"
      >
        <slot />
      </UaBtn>
    </template>
  </UaActionPrompt>
</template>
