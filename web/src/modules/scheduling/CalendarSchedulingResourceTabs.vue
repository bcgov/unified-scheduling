<script setup lang="ts">
import { computed, ref } from 'vue';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

const accessControl = useAccessControl();

const showShifts = computed(
  () =>
    (accessControl.featureFlags.value?.TimeOff?.enabled ?? false) &&
    accessControl.hasPermission(Permissions.ShiftsCreateAndAssign),
);

const showTimeOff = computed(
  () =>
    (accessControl.featureFlags.value?.TimeOff?.enabled ?? false) &&
    accessControl.hasPermission(Permissions.TimeOffCreateAndAssign),
);

const emit = defineEmits<{
  (event: 'update:activeTab', value: 'schedule' | 'timeOff'): void;
}>();

const activeTab = ref<'schedule' | 'timeOff'>('schedule');

const updateActiveTab = (value: 'schedule' | 'timeOff') => {
  activeTab.value = value;
  emit('update:activeTab', value);
};
</script>

<template>
  <div class="resource-tabs">
    <v-tabs
      :model-value="activeTab"
      class="resource-tabs__tabs"
      density="comfortable"
      @update:model-value="updateActiveTab"
    >
      <v-tab v-if="showShifts" value="schedule">Schedule</v-tab>
      <v-tab v-if="showTimeOff" value="timeOff">Time Off</v-tab>
    </v-tabs>

    <div class="resource-tabs__panels">
      <section 
         v-if="showShifts"
        class="resource-tabs__panel"
        :class="{ 'resource-tabs__panel--active': activeTab === 'schedule' }"
        :aria-hidden="activeTab !== 'schedule'"
      >
        <slot name="schedule" />
      </section>
      <section
        v-if="showTimeOff"
        class="resource-tabs__panel resource-tabs__panel--time-off"
        :class="{ 'resource-tabs__panel--active': activeTab === 'timeOff' }"
        :aria-hidden="activeTab !== 'timeOff'"
      >
        <slot name="timeOff" />
      </section>
    </div>
  </div>
</template>

<style scoped>
.resource-tabs {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.resource-tabs__tabs :deep(.v-tab) {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
}

.resource-tabs__panels {
  display: grid;
}

.resource-tabs__panel {
  grid-area: 1 / 1;
  visibility: hidden;
  pointer-events: none;
}

.resource-tabs__panel--active {
  visibility: visible;
  pointer-events: auto;
}

.resource-tabs__panel--time-off {
  align-self: start;
}
</style>
