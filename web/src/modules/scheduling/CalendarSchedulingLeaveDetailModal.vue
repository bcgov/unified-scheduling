<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import type {
  SchedulingLeaveEntryResponse as LeaveEntryResponse,
  SchedulingLeaveSeriesResponse as LeaveSeriesResponse,
} from '@/api-access/generated/models';
import { Permissions } from '@/api-access/generated/models';
import { useAccessControl } from '@/composables/useAccessControl';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import type { CalendarEventBase, CalendarRuntimeContext } from '@/modules/calendar/calendarTypes';
import {
  deleteLeaveEntry,
  deleteLeaveSeries,
  getLeaveEntry,
  getLeaveSeries,
  updateLeaveEntry,
  updateLeaveSeries,
} from '@/modules/leave/api/leaveApi';
import LeaveDetailsPanel from '@/modules/leave/components/LeaveDetailsPanel.vue';
import LeaveForm, { type LeaveFormData } from '@/modules/leave/components/LeaveForm.vue';
import { useLeaveTypeOptions } from '@/modules/leave/composables/useLeaveTypeOptions';
import { buildLeaveRequest } from '@/modules/leave/leaveRequest';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useUsersStore } from '@/stores/Users';
import type { SelectOption } from '@/types/select';
import { formatCalendarDateTimeDate, formatCalendarEventTimeRange } from '@/utils/date';
import { DateTime } from 'luxon';
import { RRule } from 'rrule';
import { isCalendarSchedulingEvent } from './calendarSchedulingData';
import { resolveCalendarEventUserIds } from './calendarSchedulingEventUsers';
import { formatAssigneeIds } from './calendarSchedulingShiftDetailRows';
import type { ShiftDetailRow } from './calendarSchedulingShiftDetailTypes';
import { formatUserOptionLabel } from './calendarSchedulingShiftForm';
import { defaultEndTime, defaultStartTime, parseFormDateTime } from './schedulingDateTime';
import { getSchedulingLifecycleCapabilities } from './schedulingLifecycle';
import { resolveSchedulingTimeZoneId } from './schedulingTimeZone';

type LeaveDetailTabId = 'details' | 'edit' | 'delete';
type LeaveOpenScope = 'event' | 'series';

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
  const eventType = event as CalendarEventBase & { leaveEntryId?: unknown };
  return (
    parsePositiveId(metadata?.leaveEntryId) ??
    parsePositiveId(eventType.leaveEntryId) ??
    parsePositiveId(event.id.match(/leave-entry\.(\d+)/)?.[1])
  );
}

function getEventSeriesId(event: CalendarEventBase): number | undefined {
  const metadata = isCalendarSchedulingEvent(event) ? event.metadata : undefined;
  const eventType = event as CalendarEventBase & { leaveSeriesId?: unknown };
  return (
    parsePositiveId(metadata?.leaveSeriesId) ??
    parsePositiveId(eventType.leaveSeriesId) ??
    parsePositiveId(event.id.match(/leave-series\.(\d+)/)?.[1])
  );
}

function getInitialOpenScope(event: CalendarEventBase): LeaveOpenScope | null {
  return getEventSeriesId(event) ? null : 'event';
}

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const accessControl = useAccessControl();
const locationsStore = useLocationsStore();
const usersStore = useUsersStore();
const { filterLeaveTypeOptionsByDate } = useLeaveTypeOptions();
const users = ref<UserResponse[]>([]);
const isLoadingUsers = ref(false);
const isSaving = ref(false);
const hasEditFormValidationError = ref(false);
const apiError = ref('');
const activeTab = ref<LeaveDetailTabId>('details');
const selectedOpenScope = ref<LeaveOpenScope | null>(getInitialOpenScope(props.event));
const selectedEntry = ref<LeaveEntryResponse | null>(null);
const selectedSeries = ref<LeaveSeriesResponse | null>(null);
const isLoadingSeries = ref(false);

const timeZoneId = computed(() =>
  resolveSchedulingTimeZoneId(
    props.event.timeZoneId,
    props.event.locationId != null ? locationsStore.entitiesMap[props.event.locationId]?.timezone : undefined,
  ),
);

const schedulingMetadata = computed(() => (isCalendarSchedulingEvent(props.event) ? props.event.metadata : undefined));
const leaveTypeName = computed(() => schedulingMetadata.value?.leaveTypeName);
const leaveEntryId = computed(() => getEventEntryId(props.event));
const leaveSeriesId = computed(() => getEventSeriesId(props.event));
const canEdit = computed(
  () =>
    (selectedOpenScope.value === 'series' ? leaveSeriesId.value != null : leaveEntryId.value != null) &&
    accessControl.hasPermission(Permissions.LeaveEdit),
);
const isDeleteConfirmed = ref(false);
const currentStatusTypeCode = computed(() =>
  selectedOpenScope.value === 'series'
    ? selectedSeries.value?.statusTypeCode
    : (selectedEntry.value?.statusTypeCode ?? props.event.statusTypeCode),
);
// Like shifts, only draft leave (a single occurrence or the whole series) can be deleted.
const canDelete = computed(
  () =>
    (selectedOpenScope.value === 'series' ? leaveSeriesId.value != null : leaveEntryId.value != null) &&
    (!currentStatusTypeCode.value || getSchedulingLifecycleCapabilities(currentStatusTypeCode.value).canDelete) &&
    accessControl.hasPermission(Permissions.LeaveDelete),
);
const shouldShowOpenScopeChoice = computed(() => leaveSeriesId.value != null && selectedOpenScope.value == null);
const entityLabel = computed(() => (selectedOpenScope.value === 'series' ? 'Leave Series' : 'Leave'));

const visibleTabs = computed<Array<{ id: LeaveDetailTabId; label: string }>>(() => [
  { id: 'details', label: 'Details' },
  ...(canEdit.value ? [{ id: 'edit' as const, label: 'Edit' }] : []),
  ...(canDelete.value ? [{ id: 'delete' as const, label: 'Delete' }] : []),
]);

const modalTitle = computed(() => {
  if (shouldShowOpenScopeChoice.value) return 'Open Leave';
  if (activeTab.value === 'edit') {
    return `Edit ${entityLabel.value}`;
  }

  if (activeTab.value === 'delete') {
    return `Delete ${entityLabel.value}`;
  }

  return `${entityLabel.value} Details`;
});

const employeeOptions = computed<SelectOption[]>(() =>
  users.value.map((user) => ({ code: user.id, description: formatUserOptionLabel(user) })),
);

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
        : (selectedEntry.value?.userIds ?? resolveCalendarEventUserIds(props.event)),
      employeeOptions.value,
    ),
  },
  { label: 'Leave Type', value: leaveTypeName.value || props.event.title || 'None' },
  {
    label: selectedOpenScope.value === 'series' ? 'Repeat' : 'Date',
    value:
      selectedOpenScope.value === 'series'
        ? formatRRuleText(selectedSeries.value?.recurrenceRule) || 'None'
        : formatDateRange(),
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

const editFormData = ref<LeaveFormData>(buildEditFormData());
const editLeaveTypeOptions = computed(() => {
  const date = editFormData.value.date;
  const dateTime = date ? DateTime.fromISO(date, { zone: timeZoneId.value }) : DateTime.now().setZone(timeZoneId.value);
  return filterLeaveTypeOptionsByDate(dateTime.isValid ? dateTime : DateTime.now(), timeZoneId.value);
});

function buildEditFormData(): LeaveFormData {
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
      userIds: selectedEntry.value.userIds,
      leaveType: selectedEntry.value.leaveTypeId,
      date: start?.date ?? '',
      startTime: start?.time ?? defaultStartTime,
      endTime: end?.time ?? null,
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
    endTime: end?.time ?? null,
    repeatMode: 'never',
    recurrenceRule: null,
    notes: props.event.notes ?? '',
  };
}

async function selectOpenScope(scope: LeaveOpenScope) {
  apiError.value = '';
  isLoadingSeries.value = true;
  try {
    const id = scope === 'series' ? leaveSeriesId.value : leaveEntryId.value;
    if (!id) {
      apiError.value = `Could not determine the leave ${scope} to open.`;
      return;
    }
    if (scope === 'series') {
      const result = await getLeaveSeries(id);
      if (result.error.value) {
        apiError.value = result.error.value.message || `Failed to load leave ${scope}.`;
        return;
      }
      selectedSeries.value = result.data.value ?? null;
      if (!selectedSeries.value) {
        apiError.value = `Leave ${scope} was not found.`;
        return;
      }
    } else {
      const result = await getLeaveEntry(id);
      if (result.error.value) {
        apiError.value = result.error.value.message || `Failed to load leave ${scope}.`;
        return;
      }
      selectedEntry.value = result.data.value ?? null;
      if (!selectedEntry.value) {
        apiError.value = `Leave ${scope} was not found.`;
        return;
      }
    }
    selectedOpenScope.value = scope;
    editFormData.value = buildEditFormData();
  } catch (error: unknown) {
    apiError.value = error instanceof Error ? error.message : `Failed to load leave ${scope}.`;
  } finally {
    isLoadingSeries.value = false;
  }
}

function selectTab(tabId: LeaveDetailTabId) {
  if (isSaving.value || shouldShowOpenScopeChoice.value || !visibleTabs.value.some((tab) => tab.id === tabId)) {
    return;
  }

  if (tabId === 'edit') {
    editFormData.value = buildEditFormData();
  }

  activeTab.value = tabId;
  apiError.value = '';
  isDeleteConfirmed.value = false;
}

async function handleSave() {
  if (selectedOpenScope.value == null) {
    return;
  }

  apiError.value = '';
  const built = buildLeaveRequest(editFormData.value, {
    locationId:
      selectedOpenScope.value === 'series'
        ? (selectedSeries.value?.locationId ?? props.event.locationId)
        : (selectedEntry.value?.locationId ?? props.event.locationId),
    timeZoneId: timeZoneId.value,
    leaveSeriesId: selectedOpenScope.value === 'event' ? (leaveSeriesId.value ?? null) : null,
    employeeOptions: employeeOptions.value,
  });
  if (built.error !== undefined) {
    apiError.value = built.error;
    return;
  }

  isSaving.value = true;
  try {
    const result =
      selectedOpenScope.value === 'series'
        ? leaveSeriesId.value && built.kind === 'series'
          ? await updateLeaveSeries(leaveSeriesId.value, built.request)
          : null
        : leaveEntryId.value && built.kind === 'entry'
          ? await updateLeaveEntry(leaveEntryId.value, built.request)
          : null;
    if (result === null) {
      apiError.value =
        selectedOpenScope.value === 'series'
          ? 'Choose a recurrence rule to update the entire series.'
          : 'Could not determine the leave occurrence to update.';
      return;
    }
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to update leave.';
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
  const id = selectedEntry.value?.id ?? leaveEntryId.value;
  if (!isDeleteConfirmed.value || selectedOpenScope.value == null || (selectedOpenScope.value === 'event' && !id)) {
    return;
  }

  apiError.value = '';
  isSaving.value = true;
  try {
    const result =
      selectedOpenScope.value === 'series' && leaveSeriesId.value
        ? await deleteLeaveSeries(leaveSeriesId.value)
        : await deleteLeaveEntry(id!);
    if (result.error.value) {
      apiError.value = result.error.value.message || 'Failed to delete leave.';
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

function formatRRuleText(value?: string | null) {
  if (!value) {
    return '';
  }

  try {
    return RRule.fromString(value).toText();
  } catch {
    return value;
  }
}
</script>

<template>
  <UaModal :title="modalTitle" width="840" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="leave-modal__scope-choice">
      <p class="leave-modal__scope-choice-text">This is one occurrence in a series. What do you want to open?</p>
      <div class="leave-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingSeries" @click="selectOpenScope('event')"
          >Only this occurrence</UaBtn
        >
        <UaBtn color="primary" variant="flat" :loading="isLoadingSeries" @click="selectOpenScope('series')">
          The entire series
        </UaBtn>
      </div>
    </div>

    <div v-else class="leave-modal">
      <div class="leave-modal__tabs" role="tablist" aria-label="Leave detail tabs">
        <button
          v-for="tab in visibleTabs"
          :key="tab.id"
          type="button"
          role="tab"
          class="leave-modal__tab"
          :class="{ 'leave-modal__tab--active': activeTab === tab.id }"
          :aria-selected="activeTab === tab.id"
          :disabled="isSaving"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <LeaveDetailsPanel
        v-if="activeTab === 'details'"
        :detail-rows="detailRows"
        :is-loading="isLoadingUsers || isLoadingSeries"
      />

      <LeaveForm
        v-else-if="activeTab === 'edit'"
        v-model="editFormData"
        show-required
        show-employees
        :disabled="isSaving"
        :employee-options="employeeOptions"
        :loading-employees="isLoadingUsers"
        :leave-type-options="editLeaveTypeOptions"
        :time-zone-id="timeZoneId"
        :show-repeat="selectedOpenScope === 'series'"
        @validation-change="hasEditFormValidationError = $event"
      />

      <section v-else class="leave-modal__delete-panel" aria-label="Delete leave panel">
        <LeaveDetailsPanel :detail-rows="detailRows" :is-loading="isLoadingUsers || isLoadingSeries" />
        <p class="leave-modal__delete-warning">
          {{
            selectedOpenScope === 'series'
              ? "This will delete all draft occurrences in the series. This can't be undone."
              : "This can't be undone."
          }}
        </p>
        <v-checkbox
          v-model="isDeleteConfirmed"
          :label="
            selectedOpenScope === 'series'
              ? 'I understand this leave series will be permanently deleted for all assigned users.'
              : 'I understand this leave will be permanently deleted for all assigned users.'
          "
          hide-details
        />
      </section>
    </div>

    <template v-if="!shouldShowOpenScopeChoice && activeTab !== 'details'" #actions>
      <template v-if="activeTab === 'delete'">
        <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Close</UaBtn>
        <UaBtn color="error" variant="flat" :disabled="!isDeleteConfirmed" :loading="isSaving" @click="handleDelete"
          >Delete</UaBtn
        >
      </template>
      <template v-else>
        <UaBtn variant="outlined" :disabled="isSaving" @click="selectTab('details')">Cancel</UaBtn>
        <UaBtn
          color="primary"
          variant="flat"
          :loading="isSaving"
          :disabled="hasEditFormValidationError"
          @click="handleSave"
        >
          Save
        </UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.leave-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.leave-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.leave-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.leave-modal__delete-panel {
  display: grid;
  gap: var(--ua-spacing-md);
}

.leave-modal__delete-warning {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.leave-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-md);
}

.leave-modal__scope-choice-text {
  margin: 0;
}

.leave-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-sm);
}
</style>
