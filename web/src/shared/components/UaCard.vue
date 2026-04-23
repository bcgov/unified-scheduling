<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Title shown in the card header. Omit for a headerless card. */
    title?: string;
    /** Override the header background color. Defaults to the theme header-green. */
    headerColor?: string;
  }>(),
  {
    title: undefined,
    headerColor: undefined,
  },
);
</script>

<template>
  <div class="ua-card" :class="{ 'ua-card--has-header': !!title || $slots.header }">
    <div
      v-if="title || $slots.header"
      class="ua-card__header"
      :style="headerColor ? { backgroundColor: headerColor } : undefined"
    >
      <slot name="header">
        <span class="ua-card__title">{{ title }}</span>
      </slot>
      <div v-if="$slots['header-actions']" class="ua-card__header-actions">
        <slot name="header-actions" />
      </div>
    </div>
    <div v-if="title || $slots.header" class="ua-card__header-strip" />
    <div class="ua-card__body">
      <slot />
    </div>
    <div v-if="$slots.actions" class="ua-card__actions">
      <slot name="actions" />
    </div>
  </div>
</template>

<style scoped>
.ua-card {
  border-radius: var(--ua-card-border-radius);
  overflow: hidden;
  background: var(--ua-card-body-bg);
}

.ua-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
  background: var(--ua-card-header-bg);
  color: var(--ua-card-header-color);
}

.ua-card__title {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.ua-card__header-strip {
  background: var(--ua-border-color);
  height: 4px;
}

.ua-card__body {
  padding: var(--ua-spacing-lg);
}

.ua-card__actions {
  display: flex;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-lg) var(--ua-spacing-xl) var(--ua-spacing-xl);
}
</style>
