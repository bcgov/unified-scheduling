<script setup lang="ts">
import type { CalendarMatrixResource } from './calendarMatrixTypes';

const props = defineProps<{
  resource: CalendarMatrixResource;
}>();

const emit = defineEmits<{
  (event: 'addResource', payload: CalendarMatrixResource): void;
}>();

function handleAddResource() {
  if (!props.resource.action) {
    return;
  }

  emit('addResource', props.resource);
}
</script>

<template>
  <div class="calendar-matrix-resource-row" role="rowheader" :aria-label="resource.title">
    <slot :resource="resource">
      <span v-if="resource.avatarText" class="calendar-matrix-resource-row__avatar">{{ resource.avatarText }}</span>
      <span class="calendar-matrix-resource-row__content">
        <span class="calendar-matrix-resource-row__title">{{ resource.title }}</span>
        <span v-if="resource.subtitle" class="calendar-matrix-resource-row__subtitle">{{ resource.subtitle }}</span>
        <span v-if="resource.meta?.length" class="calendar-matrix-resource-row__meta-list">
          <span
            v-for="(metaItem, index) in resource.meta"
            :key="`${metaItem.label ?? 'value'}-${metaItem.value}-${index}`"
            class="calendar-matrix-resource-row__meta"
          >
            <span v-if="metaItem.label" class="calendar-matrix-resource-row__meta-label"> {{ metaItem.label }}: </span>
            {{ metaItem.value }}
          </span>
        </span>
      </span>
    </slot>
    <button
      v-if="resource.action"
      class="calendar-matrix-resource-row__add"
      type="button"
      :aria-label="resource.action.ariaLabel"
      @click="handleAddResource"
    >
      <slot name="action" :resource="resource" :action="resource.action">
        <v-icon v-if="resource.action.icon" :icon="resource.action.icon" size="16" />
        <span v-else>{{ resource.action.label ?? '+' }}</span>
      </slot>
    </button>
  </div>
</template>

<style scoped>
.calendar-matrix-resource-row {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  border: 0;
  color: var(--ua-text-primary);
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: start;
  flex: 1 1 auto;
  padding: var(--ua-spacing-sm) 0 var(--ua-spacing-sm) var(--ua-spacing-md);
}

.calendar-matrix-resource-row__add {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  cursor: pointer;
  display: inline-flex;
  flex: 0 0 1.75rem;
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  height: 1.75rem;
  justify-content: center;
  line-height: 1;
  margin-right: var(--ua-spacing-md);
  width: 1.75rem;
  margin-left: auto;
}

@media (hover: hover) and (pointer: fine) {
  .calendar-matrix-resource-row__add:hover {
    background: rgb(var(--v-theme-primary) / 0.08);
    border-color: rgb(var(--v-theme-primary));
    color: rgb(var(--v-theme-primary));
  }
}

.calendar-matrix-resource-row__avatar {
  align-items: center;
  background: rgb(var(--v-theme-surface-variant));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  display: inline-flex;
  flex: 0 0 2rem;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 2rem;
  width: 2rem;
  justify-content: center;
}

.calendar-matrix-resource-row__content {
  display: grid;
  gap: 0.125rem;
}

.calendar-matrix-resource-row__title {
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  overflow-wrap: anywhere;
}

.calendar-matrix-resource-row__subtitle,
.calendar-matrix-resource-row__meta {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-xs);
  overflow-wrap: anywhere;
}

.calendar-matrix-resource-row__meta-list {
  display: grid;
  gap: 0.0625rem;
}

.calendar-matrix-resource-row__meta-label {
  font-weight: var(--ua-font-weight-semibold);
}
</style>
