<script setup lang="ts">
defineProps<{
  prompt?: string;
  parentActivator?: boolean;
}>();
</script>

<template>
  <v-tooltip
    v-if="parentActivator"
    activator="parent"
    :disabled="!prompt"
    :eager="false"
    location="bottom"
    :open-on-focus="true"
    content-class="ua-action-prompt__tooltip"
  >
    {{ prompt }}
  </v-tooltip>
  <v-tooltip
    v-else
    :disabled="!prompt"
    :eager="false"
    location="bottom"
    :open-on-focus="true"
    content-class="ua-action-prompt__tooltip"
  >
    <template #activator="{ props: tooltipProps }">
      <slot name="activator" v-bind="{ tooltipProps, highlighted: Boolean(prompt) }" />
    </template>
    {{ prompt }}
  </v-tooltip>
</template>

<style scoped>
:global(.ua-action-prompt--highlighted) {
  border: 2px solid rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 3px rgb(var(--v-theme-accent));
  font-weight: var(--ua-font-weight-bold);
}

:global(.ua-action-prompt--highlighted:focus-visible) {
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: 5px;
}

:global(.v-tooltip > .ua-action-prompt__tooltip.v-overlay__content) {
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
  max-width: min(22rem, calc(100vw - 2 * var(--ua-spacing-md)));
}
</style>
