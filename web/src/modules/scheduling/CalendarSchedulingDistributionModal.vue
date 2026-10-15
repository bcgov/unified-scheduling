<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import type { UserResponse } from '@/api-access/generated/models';
import type { CalendarPeriod } from '@/modules/calendar/calendarStore';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import type { SelectOption } from '@/types/select';
import { useUsersStore } from '@/stores/Users';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';
import type { CalendarSchedulingMatrixPayload } from './calendarSchedulingMappers';
import {
  postScheduleDistribution,
  type ScheduleDistributionPeriod,
  type ScheduleDistributionView,
} from './calendarSchedulingDistributionApi';

const props = defineProps<{
  locationId: number;
  startDate: string;
  endDate: string;
  period: CalendarPeriod;
  view: ScheduleDistributionView;
  model: CalendarMatrixViewModel;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'distributed', recipientCount: number): void;
}>();

const route = useRoute();
const router = useRouter();
const schedulingUsersStore = useUsersStore();
const selectedRecipientIds = ref<string[]>([]);
const recipientSearch = ref('');
const isLoadingUsers = ref(false);
const isSending = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

const modelPayload = computed(() => props.model.payload as CalendarSchedulingMatrixPayload | undefined);
const eligibleUsers = computed(() => schedulingUsersStore.getUsersForLocation(props.locationId));
const eligibleUsersById = computed(() => new Map(eligibleUsers.value.map((user) => [user.id, user])));

const selectedRecipients = computed(() =>
  selectedRecipientIds.value.flatMap((userId) => {
    const user = eligibleUsersById.value.get(userId);
    return user ? [user] : [];
  }),
);
const missingEmailRecipients = computed(() => selectedRecipients.value.filter((recipient) => !recipient.email?.trim()));
const recipientOptions = computed<SelectOption[]>(() => {
  const selectedIds = new Set(selectedRecipientIds.value);
  const search = recipientSearch.value.trim().toLocaleLowerCase();
  return eligibleUsers.value
    .map((user) => ({
      code: user.id,
      description: `${formatUserName(user)} (${user.email?.trim() || 'No email address'})`,
    }))
    .filter((option) => selectedIds.has(String(option.code)) || option.description.toLocaleLowerCase().includes(search))
    .sort((left, right) => left.description.localeCompare(right.description));
});

const draftCounts = computed(() => {
  let shifts = 0;
  let assignments = 0;

  for (const event of modelPayload.value?.events ?? []) {
    if (normalizeSchedulingLifecycleStatus(event.statusTypeCode) !== 'draft') {
      continue;
    }

    if (event.type === 'scheduling.assignment' || event.eventTypeCode === 'assignment') {
      assignments += 1;
    } else if (event.type === 'scheduling.shift' || event.eventTypeCode === 'shift') {
      shifts += 1;
    }
  }

  return { shifts, assignments };
});
const hasDrafts = computed(() => draftCounts.value.shifts > 0 || draftCounts.value.assignments > 0);
const draftWarningMessage = computed(
  () =>
    `This schedule contains unpublished changes. ${formatCount(draftCounts.value.shifts, 'shift')} and ${formatCount(draftCounts.value.assignments, 'assignment')} in the selected period are still in Draft status.`,
);
const conflicts = computed(() => [
  ...new Map((modelPayload.value?.conflicts ?? []).map((conflict) => [conflict.id, conflict])).values(),
]);
const unresolvedConflictCount = computed(() => conflicts.value.filter((conflict) => !conflict.isOverridden).length);
const overriddenConflictCount = computed(() => conflicts.value.filter((conflict) => conflict.isOverridden).length);
const conflictDataAvailable = computed(() => {
  return modelPayload.value?.conflictsAvailable === true;
});
const conflictWarningMessage = computed(() => {
  const parts = [];
  if (unresolvedConflictCount.value > 0) {
    parts.push(formatCount(unresolvedConflictCount.value, 'unresolved conflict'));
  }
  if (overriddenConflictCount.value > 0) {
    parts.push(formatCount(overriddenConflictCount.value, 'overridden conflict'));
  }
  return `This schedule contains ${parts.join(' and ')}.`;
});

let userLoadRequestId = 0;
watch(
  () => props.locationId,
  async (locationId) => {
    const requestId = ++userLoadRequestId;
    selectedRecipientIds.value = [];
    recipientSearch.value = '';
    isLoadingUsers.value = true;
    try {
      const users = await schedulingUsersStore.ensureUsersForLocation(locationId);
      if (requestId === userLoadRequestId) {
        const eligibleIds = new Set(users.map((user) => user.id));
        selectedRecipientIds.value = resolveDisplayedUserIds().filter((userId) => eligibleIds.has(userId));
      }
    } catch (error: unknown) {
      if (requestId === userLoadRequestId) {
        errorMessage.value = error instanceof Error ? error.message : 'Failed to load application users.';
      }
    } finally {
      if (requestId === userLoadRequestId) {
        isLoadingUsers.value = false;
      }
    }
  },
  { immediate: true },
);

function resolveDisplayedUserIds() {
  const userIds = new Set<string>();

  for (const resource of props.model.primaryColumn.resources) {
    if (resource.type === 'user') {
      userIds.add(resource.id);
    }
  }

  for (const item of props.model.sidePanel?.items ?? []) {
    if (item.type === 'user') {
      userIds.add(item.id);
    }
  }

  return [...userIds];
}

function formatUserName(user: UserResponse) {
  return [user.firstName, user.lastName].filter(Boolean).join(' ').trim() || user.idirName || user.id;
}

function printSchedule() {
  errorMessage.value = '';
  const printRoute = router.resolve({
    path: route.path,
    query: {
      view: props.view,
      mode: 'print',
      printDialog: '1',
      locationId: String(props.locationId),
      startDate: props.startDate,
      endDate: props.endDate,
      period: props.period,
    },
  });
  const printWindow = window.open(printRoute.href, '_blank');

  if (!printWindow) {
    errorMessage.value = 'The print view was blocked. Allow pop-ups for this site and try again.';
    return;
  }

  printWindow.opener = null;
}

async function sendSchedule() {
  errorMessage.value = '';
  successMessage.value = '';
  if (selectedRecipientIds.value.length === 0) {
    errorMessage.value = 'Select at least one recipient.';
    return;
  }

  if (missingEmailRecipients.value.length > 0) {
    errorMessage.value = 'Remove recipients without an email address before sending.';
    return;
  }

  isSending.value = true;
  try {
    const { data, error, execute } = postScheduleDistribution({
      context: {
        view: props.view,
        locationId: props.locationId,
        startDate: props.startDate,
        endDate: props.endDate,
        period: props.period as ScheduleDistributionPeriod,
      },
      recipientUserIds: [...selectedRecipientIds.value],
    });
    await execute();

    if (error.value) {
      errorMessage.value = error.value.message || 'Failed to distribute this schedule.';
      return;
    }

    const recipientCount = data.value?.recipientCount;
    if (typeof recipientCount !== 'number') {
      errorMessage.value = 'The schedule distribution result could not be confirmed.';
      return;
    }
    successMessage.value = `Schedule sent to ${recipientCount} recipient${recipientCount === 1 ? '' : 's'}.`;
    emit('distributed', recipientCount);
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to distribute this schedule.';
  } finally {
    isSending.value = false;
  }
}

function formatCount(count: number, noun: string) {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}
</script>

<template>
  <UaModal title="Distribute Schedule" width="720" :loading="isSending" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="errorMessage" type="error" :closable="false">{{ errorMessage }}</UaAlert>
      <UaAlert v-if="successMessage" type="success" :closable="false">{{ successMessage }}</UaAlert>
      <UaAlert v-if="hasDrafts" type="warning" :closable="false">{{ draftWarningMessage }}</UaAlert>
      <UaAlert v-if="!conflictDataAvailable" type="warning" :closable="false">
        Conflict information is unavailable for this schedule.
      </UaAlert>
      <UaAlert v-else-if="conflicts.length" type="warning" :closable="false">{{ conflictWarningMessage }}</UaAlert>
    </template>

    <div class="schedule-distribution">
      <section class="schedule-distribution__section">
        <h3 class="schedule-distribution__heading">Recipients</h3>
        <p class="schedule-distribution__help">
          Select a list of users to send a PDF copy of this schedule to. Alternatively, you can print the schedule.
        </p>

        <UaAutocomplete
          v-model="selectedRecipientIds"
          label="Recipients"
          :items="recipientOptions"
          :loading="isLoadingUsers"
          :disabled="isLoadingUsers || isSending"
          multiple
          chips
          closable-chips
          clearable
          @update:search="recipientSearch = $event"
        />

        <UaAlert v-if="missingEmailRecipients.length" type="warning" :closable="false">
          {{ missingEmailRecipients.map(formatUserName).join(', ') }}
          {{ missingEmailRecipients.length === 1 ? 'does' : 'do' }} not have an email address and cannot receive this
          distribution.
        </UaAlert>
      </section>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSending" @click="printSchedule">Print</UaBtn>
      <UaBtn
        color="primary"
        variant="flat"
        :loading="isSending"
        :disabled="selectedRecipientIds.length === 0 || missingEmailRecipients.length > 0"
        @click="sendSchedule"
      >
        Send
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.schedule-distribution,
.schedule-distribution__section {
  display: grid;
  gap: var(--ua-spacing-md);
}

.schedule-distribution__heading,
.schedule-distribution__help {
  margin: 0;
}

.schedule-distribution__heading {
  font-size: var(--ua-font-size-md);
  font-weight: var(--ua-font-weight-semibold);
}

.schedule-distribution__help {
  font-size: var(--ua-font-size-sm);
}
</style>
