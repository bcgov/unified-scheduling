<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type {
  SchedulingTimeOffEntryResponse as TimeOffEntryResponse,
  SchedulingTimeOffSeriesResponse as TimeOffSeriesResponse,
} from '@/api-access/generated/models';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import {
  deleteTimeOffEntry,
  deleteTimeOffSeries,
  getTimeOffEntry,
  getTimeOffSeries,
  updateTimeOffEntry,
  updateTimeOffSeries,
} from '@/modules/timeoff/api/timeOffApi';
import TimeOffDetailsPanel from '@/modules/timeoff/components/TimeOffDetailsPanel.vue';
import TimeOffForm, { type TimeOffFormData } from '@/modules/timeoff/components/TimeOffForm.vue';
import { buildTimeOffRequest } from '@/modules/timeoff/timeOffRequest';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useUsersStore } from '@/stores/Users';
import { formatCalendarDateTimeDate, formatCalendarEventTimeRange } from '@/utils/date';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import { resolveCalendarEventUserIds } from './calendarSchedulingEventUsers';
import { formatAssigneeIds } from './calendarSchedulingShiftDetailRows';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';
import { formatUserOptionLabel } from './calendarSchedulingShiftForm';
import { defaultEndTime, defaultStartTime, parseFormDateTime } from './schedulingDateTime';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';

type TimeOffDetailTabId = 'details' | 'edit' | 'delete';
type TimeOffOpenScope = 'event' | 'series';

const props = defineProps<{
  event: CalendarEventBase;
  runtimeContext?: CalendarRuntimeContext;
}>();

function parsePositiveId(value: unknown): number | undefined {
  const id = Number(value);
  return Number.isInteger(id) && id > 0 ? id : undefined;
}

function getEventEntryId(event: CalendarEventBase): number | undefined {
  const metadata = isCalendarSchedulingEvent(event) ? event.metadata : undefined;
  const eventType = event as CalendarEventBase & { timeOffEntryId?: unknown };
  return (
    parsePositiveId(metadata?.timeOffEntryId) ??
    parsePositiveId(eventType.timeOffEntryId) ??
    parsePositiveId(event.id.match(/timeoff-entry\.(\d+)/)?.[1])
  );
}

function getEventSeriesId(event: CalendarEventBase): number | undefined {
  const metadata = isCalendarSchedulingEvent(event) ? event.metadata : undefined;
  const eventType = event as CalendarEventBase & { timeOffSeriesId?: unknown };
  return (
    parsePositiveId(metadata?.timeOffSeriesId) ??
    parsePositiveId(eventType.timeOffSeriesId) ??
    parsePositiveId(event.id.match(/timeoff-series\.(\d+)/)?.[1])
  );
}

function getInitialOpenScope(event: CalendarEventBase): TimeOffOpenScope | null {
  return getEventSeriesId(event) ? null : 'event';
}

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const accessControl = useAccessControl();
const locationsStore = useLocationsStore();
const usersStore = useUsersStore();
const users = ref<UserResponse[]>([]);
const isLoadingUsers = ref(false);
const isSaving = ref(false);
const apiError = ref('');
const activeTab = ref<TimeOffDetailTabId>('details');
const selectedOpenScope = ref<TimeOffOpenScope | null>(getInitialOpenScope(props.event));
const selectedEntry = ref<TimeOffEntryResponse | null>(null);
const selectedSeries = ref<TimeOffSeriesResponse | null>(null);
const isLoadingSeries = ref(false);

const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    props.event.timeZoneId,
    props.event.locationId != null ? locationsStore.entitiesMap[props.event.locationId]?.timezone : undefined,
  ),
);

const schedulingMetadata = computed(() => (isCalendarSchedulingEvent(props.event) ? props.event.metadata : undefined));
const leaveTypeName = computed(() => schedulingMetadata.value?.leaveTypeName);
const timeOffEntryId = computed(() => getEventEntryId(props.event));
const timeOffSeriesId = computed(() => getEventSeriesId(props.event));
const canEdit = computed(
  () =>
    (selectedOpenScope.value === 'series' ? timeOffSeriesId.value != null : timeOffEntryId.value != null) &&
    accessControl.hasPermission(Permissions.TimeOffEdit),
);
const canDelete = computed(
  () =>
    (selectedOpenScope.value === 'series' ? timeOffSeriesId.value != null : timeOffEntryId.value != null) &&
    accessControl.hasPermission(Permissions.TimeOffDelete),
);
const shouldShowOpenScopeChoice = computed(() => timeOffSeriesId.value != null && selectedOpenScope.value == null);
const entityLabel = computed(() => (selectedOpenScope.value === 'series' ? 'Time Off Series' : 'Time Off'));

const visibleTabs = computed<Array<{ id: TimeOffDetailTabId; label: string }>>(() => [
  { id: 'details', label: 'Details' },
  ...(canEdit.value ? [{ id: 'edit' as const, label: 'Edit' }] : []),
  ...(canDelete.value ? [{ id: 'delete' as const, label: 'Delete' }] : []),
]);

const modalTitle = computed(() => {
  if (shouldShowOpenScopeChoice.value) return 'Open Time Off';
  if (activeTab.value === 'edit') {
    return `Edit ${entityLabel.value}`;
  }

  if (activeTab.value === 'delete') {
    return `Delete ${entityLabel.value}`;
  }

  return `${entityLabel.value} Details`;
});

const employeeOptions = computed(() =>
  users.value.map((user) => ({ code: user.id, description: formatUserOptionLabel(user) })),
);

const detailRows = computed<ShiftDetailRow[]>(() => [
  {
    label: 'Location',
    value: formatLocation(
      selectedSeries.value?.locationId ?? selectedEntry.value?.locationId ?? props.event.locationId,
    ),
  },
  {
    label: 'Employee(s)',
    value: formatAssigneeIds(
      selectedOpenScope.value === 'series'
        ? (selectedSeries.value?.userIds ?? [])
        : (selectedEntry.value?.assignedUserIds ?? resolveCalendarEventUserIds(props.event)),
      employeeOptions.value,
    ),
  },
  { label: 'Leave Type', value: leaveTypeName.value || props.event.title || 'None' },
  {
    label: selectedOpenScope.value === 'series' ? 'Recurrence' : 'Date',
    value: selectedOpenScope.value === 'series' ? selectedSeries.value?.recurrenceRule || 'None' : formatDateRange(),
  },
  {
    label: 'Time',
    value: formatCalendarEventTimeRange(props.event.start, props.event.end, {
      allDay: props.event.allDay,
      timeZone: timeZoneId.value,
    }),
  },
  { label: 'Notes', value: props.event.notes?.trim() || props.event.description?.trim() || 'None' },
]);

const editFormData = ref<TimeOffFormData>(buildEditFormData());

watch(
  () => props.event.locationId,
  async (locationId) => {
    if (!locationId) {
      users.value = [];
      return;
    }

    isLoadingUsers.value = true;
    try {
      users.value = await usersStore.ensureUsersForLocation(locationId);
    } catch (error: unknown) {
      users.value = [];
      apiError.value = error instanceof Error ? error.message : 'Failed to load employees.';
    } finally {
      isLoadingUsers.value = false;
    }
  },
  { immediate: true },
);

function buildEditFormData(): TimeOffFormData {
  if (selectedOpenScope.value === 'series' && selectedSeries.value) {
    const start = parseFormDateTime(selectedSeries.value.startAtUtc ?? '', timeZoneId.value);
    const end = parseFormDateTime(selectedSeries.value.endAtUtc ?? '', timeZoneId.value);
    return {
      userIds: selectedSeries.value.userIds,
      leaveType: selectedSeries.value.leaveTypeId,
      date: start?.date ?? '',
      startTime: start?.time ?? defaultStartTime,
      endTime: end?.time ?? defaultEndTime,
      repeatMode: 'custom',
      recurrenceRule: selectedSeries.value.recurrenceRule ?? null,
      notes: selectedSeries.value.notes ?? '',
    };
  }

  if (selectedEntry.value) {
    const start = parseFormDateTime(selectedEntry.value.startAtUtc ?? '', timeZoneId.value);
    const end = parseFormDateTime(selectedEntry.value.endAtUtc ?? '', timeZoneId.value);
    return {
      userIds: selectedEntry.value.assignedUserIds,
      leaveType: selectedEntry.value.leaveTypeId,
      date: start?.date ?? '',
      startTime: start?.time ?? defaultStartTime,
      endTime: end?.time ?? defaultEndTime,
      repeatMode: 'never',
      recurrenceRule: null,
      notes: selectedEntry.value.notes ?? '',
    };
  }

  const start = parseFormDateTime(props.event.start, timeZoneId.value);
  const end = props.event.end ? parseFormDateTime(props.event.end, timeZoneId.value) : null;

  return {
    userIds: resolveCalendarEventUserIds(props.event),
    leaveType: schedulingMetadata.value?.leaveTypeId ?? null,
    date: start?.date ?? '',
    startTime: start?.time ?? defaultStartTime,
    endTime: end?.time ?? defaultEndTime,
    repeatMode: 'never',
    recurrenceRule: null,
    notes: props.event.notes ?? '',
  };
}

async function selectOpenScope(scope: TimeOffOpenScope) {
  apiError.value = '';
  isLoadingSeries.value = true;
  try {
    const id = scope === 'series' ? timeOffSeriesId.value : timeOffEntryId.value;
    if (!id) {
      apiError.value = `Could not determine the time-off ${scope} to open.`;
      return;
    }
    const result = scope === 'series' ? await getTimeOffSeries(id) : await getTimeOffEntry(id);
    if (result.error.value) {
      apiError.value = result.error.value.message || `Failed to load time-off ${scope}.`;
      return;
    }
    if (scope === 'series') selectedSeries.value = result.data.value ?? null;
    else selectedEntry.value = result.data.value ?? null;
    if ((scope === 'series' && !selectedSeries.value) || (scope === 'event' && !selectedEntry.value)) {
      apiError.value = `Time-off ${scope} was not found.`;
      return;
    }
    selectedOpenScope.value = scope;
    editFormData.value = buildEditFormData();
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : `Failed to load time-off ${scope}.`;
  } finally {
    isLoadingSeries.value = false;
  }
}

function selectTab(tabId: TimeOffDetailTabId) {
  if (isSaving.value || shouldShowOpenScopeChoice.value || !visibleTabs.value.some((tab) => tab.id === tabId)) {
    return;
  }

  if (tabId === 'edit') {
    editFormData.value = buildEditFormData();
  }

  activeTab.value = tabId;
  apiError.value = '';
}

async function handleSave() {
  if (selectedOpenScope.value == null) {
    return;
  }

  apiError.value = '';
  const built = buildTimeOffRequest(editFormData.value, {
    locationId:
      selectedOpenScope.value === 'series'
        ? (selectedSeries.value?.locationId ?? props.event.locationId)
        : (selectedEntry.value?.locationId ?? props.event.locationId),
    timeZoneId: timeZoneId.value,
  });
  if (built.error !== undefined) {
    apiError.value = built.error;
    return;
  }

  isSaving.value = true;
  try {
    const result =
      selectedOpenScope.value === 'series'
        ? timeOffSeriesId.value && built.kind === 'series'
          ? await updateTimeOffSeries(timeOffSeriesId.value, built.request)
          : null
        : timeOffEntryId.value && built.kind === 'entry'
          ? await updateTimeOffEntry(timeOffEntryId.value, built.request)
          : null;
    if (result === null) {
      apiError.value =
        selectedOpenScope.value === 'series'
          ? 'Choose a recurrence rule to update the entire series.'
          : 'Could not determine the time-off occurrence to update.';
      return;
    }
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to update time off.';
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

async function handleDelete() {
  const id = selectedEntry.value?.id ?? timeOffEntryId.value;
  if (selectedOpenScope.value == null || (selectedOpenScope.value === 'event' && !id)) {
    return;
  }

  apiError.value = '';
  isSaving.value = true;
  try {
    const result =
      selectedOpenScope.value === 'series' && timeOffSeriesId.value
        ? await deleteTimeOffSeries(timeOffSeriesId.value)
        : await deleteTimeOffEntry(id!);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to delete time off.';
      return;
    }

    calendarStore.refresh();
    emit('close');
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : 'An unexpected error occurred.';
  } finally {
    isSaving.value = false;
  }
}

function formatLocation(locationId?: number) {
  if (!locationId) {
    return 'Unknown location';
  }

  const option = locationsStore.selectOptions.find((candidate) => Number(candidate.code) === locationId);
  return option?.description || 'Unknown location';
}

function formatDateRange() {
  const startDate = formatCalendarDateTimeDate(props.event.start, timeZoneId.value) || 'Unknown';
  if (!props.event.end) {
    return startDate;
  }

  const endDate = formatCalendarDateTimeDate(props.event.end, timeZoneId.value);
  return endDate && endDate !== startDate ? `${startDate} - ${endDate}` : startDate;
}
</script>

<template>
  <UaModal :title="modalTitle" width="840" @close="emit('close')">
    <template v-if="visibleTabs.length > 1" #secondary-header>
      <div class="timeoff-modal__tabs" role="tablist" aria-label="Time off detail tabs">
        <button
          v-for="tab in visibleTabs"
          :key="tab.id"
          type="button"
          role="tab"
          class="timeoff-modal__tab"
          :class="{ 'timeoff-modal__tab--active': activeTab === tab.id }"
          :aria-selected="activeTab === tab.id"
          :disabled="isSaving"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="timeoff-modal__scope-choice">
      <p class="timeoff-modal__scope-choice-text">
        This is one occurrence in a time-off series. What do you want to open?
      </p>
      <div class="timeoff-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingSeries" @click="selectOpenScope('event')">
          Only this occurrence
        </UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isLoadingSeries" @click="selectOpenScope('series')">
          The entire series
        </UaBtn>
      </div>
    </div>

    <template v-if="apiError" #alerts>
      <UaAlert type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    </template>

    <TimeOffDetailsPanel
      v-if="activeTab === 'details' && !shouldShowOpenScopeChoice"
      :detail-rows="detailRows"
      :is-loading="isLoadingUsers || isLoadingSeries"
    />

    <TimeOffForm
      v-else-if="activeTab === 'edit' && !shouldShowOpenScopeChoice"
      v-model="editFormData"
      show-employees
      :disabled="isSaving"
      :employee-options="employeeOptions"
      :loading-employees="isLoadingUsers"
      :show-repeat="selectedOpenScope === 'series'"
    />

    <section
      v-else-if="activeTab === 'delete' && !shouldShowOpenScopeChoice"
      class="timeoff-modal__delete-panel"
      aria-label="Time Off Delete Panel"
    >
      <h3 class="timeoff-modal__delete-heading">Delete {{ entityLabel.toLowerCase() }}</h3>
      <p>
        {{
          selectedOpenScope === 'series'
            ? 'Deleting the series permanently removes all draft occurrences and cannot be undone.'
            : 'Deleting this occurrence is permanent and cannot be undone.'
        }}
      </p>
    </section>

    <template v-if="activeTab !== 'details' && !shouldShowOpenScopeChoice" #actions>
      <template v-if="activeTab === 'delete'">
        <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Close</UaBtn>
        <UaBtn color="error" variant="flat" :loading="isSaving" @click="handleDelete">Delete</UaBtn>
      </template>
      <template v-else>
        <UaBtn variant="outlined" :disabled="isSaving" @click="selectTab('details')">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSave">Save</UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.timeoff-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.timeoff-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.timeoff-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.timeoff-modal__delete-panel {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.timeoff-modal__delete-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.timeoff-modal__delete-panel p {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}

.timeoff-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-md);
}

.timeoff-modal__scope-choice-text {
  margin: 0;
}

.timeoff-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-sm);
}
</style>
