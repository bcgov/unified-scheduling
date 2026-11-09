<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue';
import { DateTime } from 'luxon';
import { useRoute, useRouter } from 'vue-router';
import type { LoanOutCalendarUser, UserResponse } from '@/api-access/generated/models';
import type { CalendarPeriod } from '@/modules/calendar/calendarStore';
import type { CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import type { CalendarMatrixViewModel } from '@/modules/calendar/components/matrix/calendarMatrixTypes';
import { useLoanOutSchedulingUsers } from '@/modules/loanout/api/loanOutApi';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaAutocomplete from '@/shared/components/UaAutocomplete.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import type { SelectOption } from '@/types/select';
import { useUsersStore } from '@/stores/Users';
import { useConfigStore } from '@/stores/config';
import { addDays } from '@/utils/date';
import { canViewLoanOut } from './calendarSchedulingPermissions';
import { normalizeSchedulingLifecycleStatus } from './schedulingLifecycle';
import type { CalendarSchedulingMatrixPayload } from './calendarSchedulingMappers';
import type {
  ScheduleDistributionRequest,
  ScheduleDistributionPeriod,
  ScheduleDistributionView,
} from './calendarSchedulingDistributionApi';
import { useScheduleDistributionStore } from './calendarSchedulingDistributionStore';

const props = defineProps<{
  locationId: number;
  startDate: string;
  endDate: string;
  period: CalendarPeriod;
  view: ScheduleDistributionView;
  model: CalendarMatrixViewModel;
  runtimeContext?: CalendarRuntimeContext;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const route = useRoute();
const router = useRouter();
const schedulingUsersStore = useUsersStore();
const configStore = useConfigStore();
const distribution = useScheduleDistributionStore();
const selectedRecipientIds = ref<string[]>([]);
const recipientSearch = ref('');
const isLoadingUsers = ref(false);
const isSending = ref(false);
const includeAssignmentTimes = ref(true);
const errorMessage = ref('');
const recipientLoadError = ref('');
const borrowedUsers = ref<LoanOutCalendarUser[]>([]);
const canLoadBorrowedUsers = computed(() => Boolean(props.runtimeContext && canViewLoanOut(props.runtimeContext)));

const modelPayload = computed(() => props.model.payload as CalendarSchedulingMatrixPayload | undefined);
const eligibleUsers = computed(() => {
  const users = [
    ...(canLoadBorrowedUsers.value ? borrowedUsers.value : []),
    ...schedulingUsersStore.getUsersForLocation(props.locationId),
  ];
  return [...new Map(users.map((user) => [user.id, user])).values()].filter((user) => user.isEnabled === true);
});
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
  const zone = props.model.timeZone;
  const rangeStart = DateTime.fromISO(props.startDate, { zone });
  const rangeEnd = DateTime.fromISO(props.endDate, { zone });

  for (const event of modelPayload.value?.events ?? []) {
    if (event.locationId !== props.locationId || normalizeSchedulingLifecycleStatus(event.statusTypeCode) !== 'draft') {
      continue;
    }

    const start = DateTime.fromISO(event.start, { zone });
    const end = event.end ? DateTime.fromISO(event.end, { zone }) : start;
    // Match publication's overlap rules, using the calendar's exclusive end boundary.
    if (!(start.toMillis() < rangeEnd.toMillis() && end.toMillis() > rangeStart.toMillis())) {
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
// Keep shared home-user lookups alive across date changes; cancel this modal's consumers on close.
const homeUserLoadController = new AbortController();
onBeforeUnmount(() => homeUserLoadController.abort());
watch(
  [
    () => props.locationId,
    () => props.startDate,
    () => props.endDate,
    () => props.model.timeZone,
    canLoadBorrowedUsers,
    () => configStore.emailDistributionAvailable,
  ],
  async ([locationId, startDate, endDate, timeZoneId, loadBorrowedUsers, emailAvailable], _, onCleanup) => {
    const requestId = ++userLoadRequestId;
    const controller = new AbortController();
    onCleanup(() => {
      ++userLoadRequestId;
      controller.abort();
    });
    selectedRecipientIds.value = [];
    recipientSearch.value = '';
    borrowedUsers.value = [];
    recipientLoadError.value = '';
    errorMessage.value = '';
    isLoadingUsers.value = false;
    if (!emailAvailable) return;
    isLoadingUsers.value = true;
    try {
      const [, loanOutUsers] = await Promise.all([
        schedulingUsersStore.ensureUsersForLocation(locationId, homeUserLoadController.signal),
        loadBorrowedUsers
          ? loadBorrowedRecipients(locationId, startDate, endDate, timeZoneId, controller.signal)
          : Promise.resolve([]),
      ]);
      if (requestId === userLoadRequestId) {
        borrowedUsers.value = loanOutUsers;
        selectedRecipientIds.value = resolveDisplayedUserIds().filter((userId) => eligibleUsersById.value.has(userId));
      }
    } catch {
      if (requestId === userLoadRequestId) {
        recipientLoadError.value = 'Failed to load schedule recipients. Close and reopen this dialog to try again.';
      }
    } finally {
      if (requestId === userLoadRequestId) {
        isLoadingUsers.value = false;
      }
    }
  },
  { immediate: true },
);

async function loadBorrowedRecipients(
  locationId: number,
  startDate: string,
  endDate: string,
  timeZoneId: string,
  signal: AbortSignal,
) {
  const result = useLoanOutSchedulingUsers(
    { locationId, startDate, endDate: addDays(endDate, -1), timeZoneId },
    signal,
  );
  await result.execute();
  if (result.error.value) throw result.error.value;
  if (!result.data.value) throw new Error('Failed to load borrowed recipients.');

  const rangeStart = DateTime.fromISO(startDate, { zone: timeZoneId }).toMillis();
  const rangeEnd = DateTime.fromISO(endDate, { zone: timeZoneId }).toMillis();
  return result.data.value.filter((user) =>
    user.loanOutMetaData?.some((loanOut) => {
      if (loanOut.loanOutLocationId !== locationId || !loanOut.startAtUtc) return false;
      const start = DateTime.fromISO(loanOut.startAtUtc).toMillis();
      const end = loanOut.endAtUtc ? DateTime.fromISO(loanOut.endAtUtc).toMillis() : start;
      return start < rangeEnd && (loanOut.endAtUtc ? end > rangeStart : start >= rangeStart);
    }),
  );
}

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

function formatUserName(user: UserResponse | LoanOutCalendarUser) {
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
      ...(props.view === 'schedule' && !includeAssignmentTimes.value ? { includeAssignmentTimes: 'false' } : {}),
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
  if (!configStore.emailDistributionAvailable) return;
  if (isSending.value || distribution.isPending || isLoadingUsers.value || recipientLoadError.value) return;
  isSending.value = true;
  errorMessage.value = '';
  if (selectedRecipientIds.value.length === 0) {
    errorMessage.value = 'Select at least one recipient.';
    isSending.value = false;
    return;
  }

  if (selectedRecipientIds.value.some((userId) => !eligibleUsersById.value.has(userId))) {
    errorMessage.value = 'Some selected recipients are no longer eligible. Remove them before sending.';
    isSending.value = false;
    return;
  }

  if (missingEmailRecipients.value.length > 0) {
    errorMessage.value = 'Remove recipients without an email address before sending.';
    isSending.value = false;
    return;
  }

  try {
    const request: ScheduleDistributionRequest = {
      context: {
        view: props.view,
        locationId: props.locationId,
        startDate: props.startDate,
        endDate: props.endDate,
        period: props.period as ScheduleDistributionPeriod,
        viewportWidth: window.innerWidth,
        viewportHeight: window.innerHeight,
        ...(props.view === 'schedule' && !includeAssignmentTimes.value ? { includeAssignmentTimes: false } : {}),
      },
      recipientUserIds: [...selectedRecipientIds.value],
    };
    await nextTick();
    if (distribution.send(request)) emit('close');
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
      <UaAlert v-if="recipientLoadError" type="error" :closable="false">{{ recipientLoadError }}</UaAlert>
      <UaAlert v-if="errorMessage" type="error" :closable="false">{{ errorMessage }}</UaAlert>
      <UaAlert v-if="hasDrafts" type="warning" :closable="false">{{ draftWarningMessage }}</UaAlert>
      <UaAlert v-if="!conflictDataAvailable" type="warning" :closable="false">
        Conflict information is unavailable for this schedule.
      </UaAlert>
      <UaAlert v-else-if="conflicts.length" type="warning" :closable="false">{{ conflictWarningMessage }}</UaAlert>
    </template>

    <div class="schedule-distribution">
      <section class="schedule-distribution__section">
        <h3 v-if="configStore.emailDistributionAvailable" class="schedule-distribution__heading">Recipients</h3>
        <p v-if="configStore.emailDistributionAvailable" class="schedule-distribution__help">
          Select a list of users to send a PDF copy of this schedule to. Alternatively, you can print the schedule.
        </p>

        <UaAutocomplete
          v-if="configStore.emailDistributionAvailable"
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

        <v-switch
          v-if="view === 'schedule'"
          v-model="includeAssignmentTimes"
          label="Include Assignment Times"
          color="primary"
          hide-details
          inset
          :disabled="isSending"
        />

        <UaAlert
          v-if="configStore.emailDistributionAvailable && missingEmailRecipients.length"
          type="warning"
          :closable="false"
        >
          {{ missingEmailRecipients.map(formatUserName).join(', ') }}
          {{ missingEmailRecipients.length === 1 ? 'does' : 'do' }} not have an email address and cannot receive this
          distribution.
        </UaAlert>
      </section>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSending" @click="printSchedule">Print</UaBtn>
      <UaBtn
        v-if="configStore.emailDistributionAvailable"
        color="primary"
        variant="flat"
        :aria-busy="isSending"
        :disabled="
          isSending ||
          distribution.isPending ||
          isLoadingUsers ||
          !!recipientLoadError ||
          selectedRecipientIds.length === 0 ||
          missingEmailRecipients.length > 0
        "
        @click="sendSchedule"
      >
        <v-progress-circular v-if="isSending" indeterminate size="16" width="2" class="mr-2" aria-hidden="true" />
        {{ isSending ? 'Starting distribution…' : 'Send' }}
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
