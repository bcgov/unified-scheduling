<script setup lang="ts">
const { title } = defineProps<{
  title: string;
}>();
</script>

<template>
  <div class="ua-page-header">
    <h2 class="ua-page-header__title">{{ title }}</h2>
    <div v-if="$slots.actions" class="ua-page-header__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.ua-page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ua-spacing-md) 0;
}

.ua-page-header__title {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}
</style>
