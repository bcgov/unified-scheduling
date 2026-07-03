<script setup lang="ts">
import { computed } from 'vue';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import UaModal from '@/shared/components/UaModal.vue';

const props = defineProps<{
  event: CalendarEventBase;
}>();

const emit = defineEmits<{
  close: [];
}>();

const eventActionDetails = computed(() =>
  Object.entries(props.event).map(([name, value]) => ({
    name: formatLabel(name),
    value: formatValue(value),
  })),
);

function formatLabel(value: string) {
  return value.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (firstLetter) => firstLetter.toUpperCase());
}

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
  <UaModal title="Event Action" width="640" @close="emit('close')">
    <section class="calendar-scheduling-event-action-modal">
      <p class="calendar-scheduling-event-action-modal__text">clicked add on event with details:</p>
      <dl class="calendar-scheduling-event-action-modal__list">
        <template v-for="detail in eventActionDetails" :key="detail.name">
          <dt>{{ detail.name }}</dt>
          <dd>{{ detail.value }}</dd>
        </template>
      </dl>
    </section>
  </UaModal>
</template>

<style scoped>
.calendar-scheduling-event-action-modal {
  display: grid;
  gap: var(--ua-spacing-md);
}

.calendar-scheduling-event-action-modal__text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  margin: 0;
}

.calendar-scheduling-event-action-modal__list {
  display: grid;
  gap: var(--ua-spacing-xs) var(--ua-spacing-md);
  grid-template-columns: max-content minmax(0, 1fr);
  margin: 0;
}

.calendar-scheduling-event-action-modal__list dt {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
}

.calendar-scheduling-event-action-modal__list dd {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
  overflow-wrap: anywhere;
}
</style>
