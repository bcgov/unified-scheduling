<script setup lang="ts">
defineProps<{
  icon?: string;
  text?: string;
}>();

const emit = defineEmits<{
  resolve: [];
}>();
</script>

<template>
  <section class="calendar-scheduling-conflict-overlay">
    <div class="calendar-scheduling-conflict-overlay__summary">
      <v-icon v-if="icon" :icon="icon" size="18" />
      <span>{{ text ?? 'placeholder' }}</span>
    </div>
    <button class="calendar-scheduling-conflict-overlay__resolve" type="button" @click.stop="emit('resolve')">
      Resolve
    </button>
  </section>
</template>

<style scoped>
.calendar-scheduling-conflict-overlay {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgb(var(--v-theme-error));
  border-radius: 4px;
  color: var(--ua-text-primary);
  display: grid;
  gap: var(--ua-spacing-sm);
  left: 0;
  padding: var(--ua-spacing-sm);
  position: absolute;
  right: 0;
  top: 1.875rem;
  z-index: 20;
}

.calendar-scheduling-conflict-overlay__summary {
  align-items: center;
  color: rgb(var(--v-theme-error));
  display: flex;
  font-size: var(--ua-font-size-sm);
  gap: var(--ua-spacing-xs);
  line-height: 1.25;
}

.calendar-scheduling-conflict-overlay__resolve {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgb(var(--v-theme-error));
  border-radius: 4px;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-sm);
  line-height: 1.25;
  padding: var(--ua-spacing-sm);
}
</style>
