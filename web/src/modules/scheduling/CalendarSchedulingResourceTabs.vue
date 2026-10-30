<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';

const accessControl = useAccessControl();
const props = withDefaults(
  defineProps<{
    allowLeave?: boolean;
    allowLoanOut?: boolean;
  }>(),
  { allowLeave: true, allowLoanOut: true },
);

const showShifts = computed(
  () =>
    (accessControl.featureFlags.value?.Scheduling?.enabled ?? false) &&
    accessControl.hasPermission(Permissions.ShiftsCreateAndAssign),
);

const showLeave = computed(
  () =>
    props.allowLeave &&
    (accessControl.featureFlags.value?.Leave?.enabled ?? false) &&
    accessControl.hasPermission(Permissions.LeaveCreateAndAssign),
);
const showLoanOut = computed(
  () =>
    props.allowLoanOut &&
    (accessControl.featureFlags.value?.LoanOut?.enabled ?? false) &&
    accessControl.hasPermission(Permissions.LoanOutCreateAndAssign),
);

type ResourceTab = 'schedule' | 'leave' | 'loanout';
const emit = defineEmits<{
  (event: 'update:activeTab', value: ResourceTab): void;
}>();

const activeTab = ref<ResourceTab>('schedule');

const updateActiveTab = (value: ResourceTab) => {
  activeTab.value = value;
  emit('update:activeTab', value);
};
watch(
  [showShifts, showLeave, showLoanOut],
  () => {
    const available = [
      ...(showShifts.value ? ['schedule' as const] : []),
      ...(showLeave.value ? ['leave' as const] : []),
      ...(showLoanOut.value ? ['loanout' as const] : []),
    ];
    if (!available.includes(activeTab.value) && available[0]) updateActiveTab(available[0]);
  },
  { immediate: true },
);
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
      <v-tab v-if="showLeave" value="leave">Leave</v-tab>
      <v-tab v-if="showLoanOut" value="loanout">Loan Out</v-tab>
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
        v-if="showLeave"
        class="resource-tabs__panel resource-tabs__panel--leave"
        :class="{ 'resource-tabs__panel--active': activeTab === 'leave' }"
        :aria-hidden="activeTab !== 'leave'"
      >
        <slot name="leave" />
      </section>
      <section
        v-if="showLoanOut"
        class="resource-tabs__panel"
        :class="{ 'resource-tabs__panel--active': activeTab === 'loanout' }"
        :aria-hidden="activeTab !== 'loanout'"
      >
        <slot name="loanout" />
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

.resource-tabs__panel--leave {
  align-self: start;
}
</style>
