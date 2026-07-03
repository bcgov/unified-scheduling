<script setup lang="ts">
import { computed } from 'vue';
import UaModal from '@/shared/components/UaModal.vue';
import type { CalendarSchedulingDropModalState } from './calendarSchedulingState';

const props = defineProps<{
  state: CalendarSchedulingDropModalState;
}>();

const emit = defineEmits<{
  close: [];
}>();

const sourceDetails = computed(() => [
  { name: 'Source', value: props.state.drag.source },
  { name: 'Item ID', value: props.state.drag.itemId },
  { name: 'Item Type', value: props.state.drag.itemType },
  { name: 'Payload', value: formatValue(props.state.drag.payload) },
]);

const targetDetails = computed(() => [
  { name: 'Resource ID', value: formatValue(props.state.drop.resourceId) },
  { name: 'Resource Type', value: formatValue(props.state.drop.resourceType) },
  { name: 'Date', value: formatValue(props.state.drop.date) },
]);

function formatValue(value: unknown) {
  if (value === undefined || value === null || value === '') {
    return 'None';
  }

  if (typeof value === 'string') {
    return value;
  }

  return JSON.stringify(value);
}
</script>

<template>
  <UaModal title="Scheduling Drop" width="560" @close="emit('close')">
    <section class="calendar-scheduling-drop-modal">
      <div class="calendar-scheduling-drop-modal__section">
        <h3 class="calendar-scheduling-drop-modal__heading">Source</h3>
        <dl class="calendar-scheduling-drop-modal__list">
          <template v-for="detail in sourceDetails" :key="detail.name">
            <dt>{{ detail.name }}</dt>
            <dd>{{ detail.value }}</dd>
          </template>
        </dl>
      </div>

      <div class="calendar-scheduling-drop-modal__section">
        <h3 class="calendar-scheduling-drop-modal__heading">Target</h3>
        <dl class="calendar-scheduling-drop-modal__list">
          <template v-for="detail in targetDetails" :key="detail.name">
            <dt>{{ detail.name }}</dt>
            <dd>{{ detail.value }}</dd>
          </template>
        </dl>
      </div>
    </section>
  </UaModal>
</template>

<style scoped>
.calendar-scheduling-drop-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.calendar-scheduling-drop-modal__section {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.calendar-scheduling-drop-modal__heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.calendar-scheduling-drop-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-scheduling-drop-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-scheduling-drop-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}
</style>
