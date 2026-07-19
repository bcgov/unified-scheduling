<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { DateTime } from 'luxon';
import type { CalendarEventBase } from '@/modules/calendar/calendarTypes';
import { useCalendarStore } from '@/modules/calendar/calendarStore';
import { useLocationsStore } from '@/stores/LocationsStore';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';
import CalendarSchedulingShiftDeletePanel from './CalendarSchedulingShiftDeletePanel.vue';
import CalendarSchedulingShiftDetailsPanel from './CalendarSchedulingShiftDetailsPanel.vue';
import CalendarSchedulingShiftEditPanel from './CalendarSchedulingShiftEditPanel.vue';
import {
  createShiftFormDataFromEvent,
  createShiftFormDataFromSeries,
  normalizeTimeOptionValue,
  type ShiftResourceFormData,
} from './calendarSchedulingShiftForm';
import type { ShiftDetailTabId, ShiftOpenScope } from './calendarSchedulingShiftDetailTypes';
import { resolveShiftEntryId, resolveShiftSeriesId } from './calendarSchedulingShiftIds';
import { loadShiftEntry } from './calendarSchedulingShiftApi';
import { useSchedulingAssignmentOptions } from './useSchedulingAssignmentOptions';
import { useSchedulingEmployeeOptions } from './useSchedulingEmployeeOptions';
import { useSchedulingShiftDelete } from './useSchedulingShiftDelete';
import { useSchedulingShiftDetailRows } from './useSchedulingShiftDetailRows';
import { useSchedulingShiftMutation } from './useSchedulingShiftMutation';
import { useSchedulingShiftSeriesLoader } from './useSchedulingShiftSeriesLoader';

const props = defineProps<{
  event: CalendarEventBase;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
}>();

const calendarStore = useCalendarStore();
const locationsStore = useLocationsStore();

const tabs: Array<{ id: ShiftDetailTabId; label: string }> = [
  { id: 'details', label: 'Details' },
  { id: 'edit', label: 'Edit' },
  { id: 'transfer', label: 'Transfer' },
  { id: 'copy', label: 'Copy' },
  { id: 'delete', label: 'Delete' },
];
const publishedShiftMessage = 'This shift has been published, and cannot be edited or deleted, only cancelled';

const activeTab = ref<ShiftDetailTabId>('details');
const selectedOpenScope = ref<ShiftOpenScope | null>(getInitialOpenScope());
const placeholderNotice = ref('');
const modalError = ref('');
const isLoadingShiftEntry = ref(false);
const eventRef = computed(() => props.event);
const timeZoneId = computed(() => props.event.timeZoneId || Intl.DateTimeFormat().resolvedOptions().timeZone);
const editFormData = ref<ShiftResourceFormData>(createEditFormData());

const { selectedSeries, isLoadingSeries, loadSelectedSeries, clearSelectedSeries } = useSchedulingShiftSeriesLoader({
  event: eventRef,
  onError: setApiError,
});

const activeTimeZoneId = computed(() =>
  selectedOpenScope.value === 'series' ? selectedSeries.value?.timeZoneId || timeZoneId.value : timeZoneId.value,
);
const activeLocationId = computed<number | null>(() => {
  if (editFormData.value.locationId != null) {
    return normalizeLocationId(editFormData.value.locationId);
  }

  if (selectedOpenScope.value === 'series' && selectedSeries.value?.locationId != null) {
    return selectedSeries.value.locationId;
  }

  if (props.event.locationId != null) {
    return props.event.locationId;
  }

  const candidate = locationsStore.selectedLocationId;

  if (candidate === '' || candidate == null) {
    return null;
  }

  const parsedLocationId = Number(candidate);
  return Number.isFinite(parsedLocationId) ? parsedLocationId : null;
});
const eventBelongsToSeries = computed(() => resolveShiftSeriesId(props.event) !== null);
const shouldShowOpenScopeChoice = computed(() => eventBelongsToSeries.value && selectedOpenScope.value === null);
const isSeriesScope = computed(() => selectedOpenScope.value === 'series');
const selectedShiftStatusTypeCode = computed(() =>
  isSeriesScope.value && selectedSeries.value?.statusTypeCode ? selectedSeries.value.statusTypeCode : props.event.statusTypeCode,
);
const isPublishedShift = computed(() => String(selectedShiftStatusTypeCode.value ?? '').toLowerCase() === 'active');
const canEditSelectedShift = computed(() => String(selectedShiftStatusTypeCode.value ?? '').toLowerCase() === 'draft');
const visibleTabs = computed(() => tabs.filter((tab) => tab.id !== 'edit' || canEditSelectedShift.value));
const shiftEntityLabel = computed(() => (isSeriesScope.value ? 'Shift Series' : 'Shift'));
const modalTitle = computed(() => {
  if (shouldShowOpenScopeChoice.value) {
    return 'Open Shift';
  }

  if (activeTab.value === 'edit') {
    return `Edit ${shiftEntityLabel.value}`;
  }

  if (activeTab.value === 'delete') {
    const statusTypeCode =
      isSeriesScope.value && selectedSeries.value?.statusTypeCode
        ? selectedSeries.value.statusTypeCode
        : props.event.statusTypeCode;

    return `${statusTypeCode?.toLowerCase() === 'active' ? 'Cancel' : 'Delete'} ${shiftEntityLabel.value}`;
  }

  return `${shiftEntityLabel.value} Details`;
});
const modalWidth = computed(() => (shouldShowOpenScopeChoice.value ? 420 : 840));

const { employeeOptions, isLoadingUsers } = useSchedulingEmployeeOptions(activeLocationId, editFormData, {
  onError: setApiError,
});
const { assignmentEntryOptions, assignmentSeriesOptions, assignmentWarning, isLoadingAssignments } =
  useSchedulingAssignmentOptions({
    formData: editFormData,
    activeLocationId,
    activeTimeZoneId,
    isSeriesScope,
    onError: setApiError,
  });

const {
  apiError: mutationError,
  formErrors,
  isSaving: isMutating,
  clearErrors: clearMutationErrors,
  handleRecurrenceChange,
  handleRecurrenceInvalid,
  saveShift,
} = useSchedulingShiftMutation({
  event: eventRef,
  formData: editFormData,
  selectedOpenScope,
  activeTimeZoneId,
  activeLocationId,
  existingRecurrenceRule: computed(() => selectedSeries.value?.recurrenceRule ?? null),
});

const {
  canDeleteShift,
  deleteActionLabel,
  deleteConfirmationLabel,
  deleteDisabledReason,
  deleteError,
  deleteWarning,
  isDeleteConfirmed,
  isDeleting,
  clearDeleteState,
  deleteShift,
} = useSchedulingShiftDelete({
  event: eventRef,
  selectedOpenScope,
  selectedSeries,
});

const { detailRows } = useSchedulingShiftDetailRows({
  event: eventRef,
  selectedOpenScope,
  selectedSeries,
  formData: editFormData,
  employeeOptions,
  assignmentEntryOptions,
  assignmentSeriesOptions,
  locationOptions: computed(() => locationsStore.selectOptions),
  activeTimeZoneId,
});
const apiError = computed(() => modalError.value || mutationError.value || deleteError.value);
const isSaving = computed(() => isMutating.value || isDeleting.value);

watch(
  () => props.event,
  (event) => {
    clearSelectedSeries();
    editFormData.value = createShiftFormDataFromEvent(event, timeZoneId.value);
    activeTab.value = 'details';
    selectedOpenScope.value = getInitialOpenScope();
    placeholderNotice.value = '';
    clearApiError();
    void loadSelectedShiftEntryDetails();
  },
  { immediate: true },
);

watch(
  () => editFormData.value.repeatMode,
  (value) => {
    if (value === 'never') {
      editFormData.value.recurrenceRule = null;
      handleRecurrenceChange(null);
    }
  },
);

const placeholderHeading = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Edit Shift';
    case 'transfer':
      return 'Transfer Shift';
    case 'copy':
      return 'Copy Shift';
    case 'delete':
      return 'Delete Shift';
    default:
      return 'Shift Details';
  }
});

const placeholderDescription = computed(() => {
  switch (activeTab.value) {
    case 'edit':
      return 'Editing is not implemented yet.';
    case 'transfer':
      return 'Transfer is not implemented yet.';
    case 'copy':
      return 'Copy is not implemented yet.';
    case 'delete':
      return 'Delete is not implemented yet. This tab is a placeholder for the future delete flow.';
    default:
      return '';
  }
});

function selectTab(tabId: ShiftDetailTabId) {
  if (tabId === 'edit' && !canEditSelectedShift.value) {
    return;
  }

  activeTab.value = tabId;
  placeholderNotice.value = '';
  clearApiError();
  clearDeleteState();
}

function normalizeLocationId(value: unknown) {
  const parsedLocationId = Number(value);
  return Number.isInteger(parsedLocationId) && parsedLocationId > 0 ? parsedLocationId : null;
}

async function selectOpenScope(scope: ShiftOpenScope) {
  clearApiError();
  placeholderNotice.value = '';

  if (scope === 'series') {
    const series = await loadSelectedSeries();
    if (!series) {
      return;
    }
  } else {
    clearSelectedSeries();
  }

  selectedOpenScope.value = scope;
  editFormData.value = createEditFormData();
  if (scope === 'event') {
    void loadSelectedShiftEntryDetails();
  }
  activeTab.value = 'details';
}

function createEditFormData(): ShiftResourceFormData {
  if (selectedOpenScope.value === 'series' && selectedSeries.value) {
    return createShiftFormDataFromSeries(selectedSeries.value, props.event, activeTimeZoneId.value);
  }

  return createShiftFormDataFromEvent(props.event, timeZoneId.value);
}

async function loadSelectedShiftEntryDetails() {
  if (selectedOpenScope.value !== 'event') {
    return;
  }

  const shiftEntryId = resolveShiftEntryId(props.event);
  if (!shiftEntryId) {
    return;
  }

  isLoadingShiftEntry.value = true;

  try {
    const result = await loadShiftEntry(shiftEntryId);
    if (result.error.value) {
      setApiError(result.error.value.message || 'Failed to load shift assignment links.');
      return;
    }

    const shiftEntry = result.data.value;
    const assignmentEntryLinks = (shiftEntry?.assignmentLinks ?? []).flatMap((link) =>
      typeof link.assignmentEntryId === 'number'
        ? [
            {
              assignmentEntryId: link.assignmentEntryId,
              assignedUserIds: link.userIds ?? editFormData.value.userIds ?? [],
            },
          ]
        : [],
    );
    const shiftEntryTimeZoneId = shiftEntry?.timeZoneId || activeTimeZoneId.value;
    const shiftEntryStart = shiftEntry?.startAtUtc
      ? DateTime.fromISO(shiftEntry.startAtUtc, { zone: shiftEntryTimeZoneId })
      : null;
    const shiftEntryEnd = shiftEntry?.endAtUtc
      ? DateTime.fromISO(shiftEntry.endAtUtc, { zone: shiftEntryTimeZoneId })
      : null;

    editFormData.value = {
      ...editFormData.value,
      title: shiftEntry?.title ?? editFormData.value.title,
      date: shiftEntryStart?.isValid ? shiftEntryStart.toFormat('yyyy-MM-dd') : editFormData.value.date,
      startTime: shiftEntryStart?.isValid
        ? normalizeTimeOptionValue(shiftEntryStart.toFormat('HH:mm'))
        : editFormData.value.startTime,
      endTime: shiftEntryEnd?.isValid
        ? normalizeTimeOptionValue(shiftEntryEnd.toFormat('HH:mm'))
        : editFormData.value.endTime,
      timeZoneId: shiftEntry?.timeZoneId ?? editFormData.value.timeZoneId,
      statusTypeCode: shiftEntry?.statusTypeCode ?? editFormData.value.statusTypeCode,
      locationId: shiftEntry?.locationId ?? editFormData.value.locationId,
      userIds: shiftEntry?.userIds ?? editFormData.value.userIds,
      assignmentEntryLinks,
      assignmentEntryIds: assignmentEntryLinks.map((link) => link.assignmentEntryId),
    };
  } catch (error: unknown) {
    setApiError(error instanceof Error ? error.message : 'Failed to load shift assignment links.');
  } finally {
    isLoadingShiftEntry.value = false;
  }
}

async function handleSaveEdit() {
  const saved = await saveShift();

  if (saved) {
    calendarStore.refresh();
    emit('close');
  }
}

async function handleDeleteShift() {
  const deleted = await deleteShift();

  if (deleted) {
    calendarStore.refresh();
    emit('close');
  }
}

function getInitialOpenScope(): ShiftOpenScope | null {
  return resolveShiftSeriesId(props.event) ? null : 'event';
}

function setApiError(message: string) {
  modalError.value = message;
}

function clearApiError() {
  modalError.value = '';
  clearMutationErrors();
  clearDeleteState();
}
</script>

<template>
  <UaModal :title="modalTitle" :width="modalWidth" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="clearApiError">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="placeholderNotice" type="info" @close="placeholderNotice = ''">
        {{ placeholderNotice }}
      </UaAlert>
      <UaAlert v-if="isPublishedShift" type="info">
        {{ publishedShiftMessage }}
      </UaAlert>
    </template>

    <div v-if="shouldShowOpenScopeChoice" class="shift-detail-modal__scope-choice">
      <p class="shift-detail-modal__scope-choice-text">This is one event in a series. What do you want to open?</p>
      <div class="shift-detail-modal__scope-choice-actions">
        <UaBtn variant="outlined" :disabled="isLoadingSeries" @click="selectOpenScope('event')">Only This Event</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isLoadingSeries" @click="selectOpenScope('series')">
          The Entire Series
        </UaBtn>
      </div>
    </div>

    <div v-else class="shift-detail-modal">
      <div class="shift-detail-modal__tabs" role="tablist" aria-label="Shift Detail Tabs">
        <button
          v-for="tab in visibleTabs"
          :key="tab.id"
          :aria-selected="tab.id === activeTab"
          class="shift-detail-modal__tab"
          :class="{ 'shift-detail-modal__tab--active': tab.id === activeTab }"
          role="tab"
          type="button"
          @click="selectTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <CalendarSchedulingShiftDetailsPanel
        v-if="activeTab === 'details'"
        :detail-rows="detailRows"
        :is-loading="isLoadingShiftEntry"
      />

      <CalendarSchedulingShiftEditPanel
        v-else-if="activeTab === 'edit'"
        v-model="editFormData"
        :form-errors="formErrors"
        :disabled="isSaving"
        :show-recurrence="isSeriesScope"
        :location-options="locationsStore.selectOptions"
        :employee-options="employeeOptions"
        :is-loading-users="isLoadingUsers"
        :assignment-entry-options="assignmentEntryOptions"
        :assignment-series-options="assignmentSeriesOptions"
        :assignment-warning="assignmentWarning"
        :is-loading-assignments="isLoadingAssignments || isLoadingShiftEntry"
        :show-series-assignment="isSeriesScope"
        @recurrence-change="handleRecurrenceChange"
        @recurrence-invalid="handleRecurrenceInvalid"
      />

      <CalendarSchedulingShiftDeletePanel
        v-else-if="activeTab === 'delete'"
        v-model:is-delete-confirmed="isDeleteConfirmed"
        :detail-rows="detailRows"
        :delete-confirmation-label="deleteConfirmationLabel"
        :delete-disabled-reason="deleteDisabledReason"
        :delete-warning="deleteWarning"
      />

      <section v-else class="shift-detail-modal__panel" :aria-label="`${placeholderHeading} Panel`">
        <div class="shift-detail-modal__placeholder">
          <h3 class="shift-detail-modal__placeholder-heading">{{ placeholderHeading }}</h3>
          <p class="shift-detail-modal__placeholder-text">{{ placeholderDescription }}</p>
        </div>
      </section>
    </div>

    <template v-if="!shouldShowOpenScopeChoice && (activeTab === 'edit' || activeTab === 'delete')" #actions>
      <template v-if="activeTab === 'edit'">
        <UaBtn variant="outlined" :disabled="isSaving" @click="selectTab('details')">Cancel</UaBtn>
        <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSaveEdit">Save</UaBtn>
      </template>
      <template v-else>
        <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Close</UaBtn>
        <UaBtn color="error" variant="flat" :disabled="!canDeleteShift" :loading="isSaving" @click="handleDeleteShift">
          {{ deleteActionLabel }}
        </UaBtn>
      </template>
    </template>
  </UaModal>
</template>

<style scoped>
.shift-detail-modal {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice {
  display: grid;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__scope-choice-text {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.shift-detail-modal__scope-choice-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-md);
  justify-content: flex-end;
}

.shift-detail-modal__tabs {
  display: flex;
  flex-wrap: wrap;
  gap: var(--ua-spacing-lg);
}

.shift-detail-modal__tab {
  background: transparent;
  border: 0;
  border-bottom: 2px solid transparent;
  color: var(--ua-text-primary);
  cursor: pointer;
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-semibold);
  padding: 0 0 var(--ua-spacing-xs);
}

.shift-detail-modal__tab--active {
  border-bottom-color: rgb(var(--v-theme-primary));
}

.shift-detail-modal__placeholder {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  display: grid;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-lg);
}

.shift-detail-modal__placeholder-heading {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-base);
  font-weight: var(--ua-font-weight-bold);
  margin: 0;
}

.shift-detail-modal__placeholder-text {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  margin: 0;
}
</style>
