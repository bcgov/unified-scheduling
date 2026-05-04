<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Number of columns for labels. Defaults to form-label-width token. */
    labelWidth?: string;
  }>(),
  {
    labelWidth: 'var(--ua-form-label-width)',
  },
);
</script>

<template>
  <div class="ua-form-grid" :style="{ gridTemplateColumns: `${labelWidth} 1fr` }">
    <slot />
  </div>
</template>

<style scoped>
.ua-form-grid {
  display: grid;
  gap: var(--ua-spacing-md);
  align-items: center;
}

@media (max-width: 640px) {
  .ua-form-grid {
    grid-template-columns: 1fr !important;
    gap: var(--ua-spacing-xs) 0;
  }
}
</style>
