<script setup lang="ts">
import type { CalendarViewDefinition } from '../registry/calendarRegistryTypes';

defineProps<{
  views: CalendarViewDefinition[];
  activeViewId: string;
}>();

const emit = defineEmits<{
  (event: 'update:activeViewId', viewId: string): void;
}>();
</script>

<template>
  <div class="calendar-view-tabs" role="tablist" aria-label="Calendar views">
    <button
      v-for="view in views"
      :key="view.id"
      :aria-selected="view.id === activeViewId"
      class="calendar-view-tabs__tab"
      :class="{ 'calendar-view-tabs__tab--active': view.id === activeViewId }"
      role="tab"
      type="button"
      @click="emit('update:activeViewId', view.id)"
    >
      {{ view.label }}
    </button>
  </div>
</template>

<style scoped>
.calendar-view-tabs {
  display: flex;
  gap: var(--ua-spacing-sm);
  margin-bottom: var(--ua-spacing-md);
}

.calendar-view-tabs__tab {
  align-items: center;
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background: rgb(var(--v-theme-surface));
  color: var(--ua-text-primary);
  cursor: pointer;
  display: inline-flex;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  min-height: var(--calendar-toolbar-control-height);
  padding: 0 var(--ua-spacing-lg);
}

.calendar-view-tabs__tab--active {
  background: rgb(var(--v-theme-primary));
  border-color: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
}
</style>
