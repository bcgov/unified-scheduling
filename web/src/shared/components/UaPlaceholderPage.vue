<script setup lang="ts">
const { title, description } = withDefaults(
  defineProps<{
    title: string;
    description?: string;
  }>(),
  {
    description: 'This feature is coming soon.',
  },
);
</script>

<template>
  <div class="ua-placeholder-page">
    <h2 class="ua-placeholder-page__title">{{ title }}</h2>
    <p class="ua-placeholder-page__description">{{ description }}</p>
    <slot />
  </div>
</template>

<style scoped>
.ua-placeholder-page {
  padding: var(--ua-spacing-xl);
}

.ua-placeholder-page__title {
  font-size: var(--ua-font-size-xl);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
  margin-bottom: var(--ua-spacing-md);
}

.ua-placeholder-page__description {
  color: var(--ua-text-muted);
}
</style>
